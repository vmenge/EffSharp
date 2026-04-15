# Concurrency Proposal

## Goal

Add structured concurrency to EffSharp built on .NET's `Task` system. No custom scheduler. .NET's ThreadPool already provides cooperative scheduling, work-stealing, and thread management -- the same infrastructure that makes ASP.NET one of the fastest web frameworks.

Two fork primitives, a fiber handle type, and derived combinators (`race`, `all`, `timeout`). Cancellation via `CancellationToken` internally, never exposed to user code. A 4th exit channel (`Interrupted`) that cleanup frames respect but `catch` does not intercept.

---

## API Surface

### Types

```fsharp
type Fiber<'t, 'e>

type Exit<'t, 'e> =
  | Ok of 't
  | Err of 'e
  | Exn of exn
  | Interrupted          // new — intentional cancellation, not a defect
```

### Fiber Operations

```fsharp
module Fiber =
  /// Await the fiber's result. Routes the child's three-channel exit
  /// (Ok/Err/Exn) into the caller's channels. If the child was
  /// interrupted, the caller receives Exit.Interrupted.
  val await : Fiber<'t, 'e> -> Eff<'t, 'e, 'env>

  /// Observe the fiber's outcome without re-raising.
  /// Always succeeds with an Exit value.
  val join  : Fiber<'t, 'e> -> Eff<Exit<'t, 'e>, 'e2, 'env>

  /// Signal the fiber to stop. Waits for cleanup (defer/bracket)
  /// to complete before returning.
  val abort : Fiber<'t, 'e> -> Eff<unit, 'e2, 'env>
```

### Fork Primitives

```fsharp
module Eff =
  /// Start an effect concurrently. The child begins on the caller's
  /// thread and yields at the first async boundary. Returns a fiber
  /// handle. Best for I/O-bound work.
  val fork   : Eff<'t, 'e, 'env> -> Eff<Fiber<'t, 'e>, 'e2, 'env>

  /// Start an effect on the ThreadPool via Task.Run. The child
  /// immediately moves to a ThreadPool thread. Returns a fiber
  /// handle. Best for CPU-bound or blocking work.
  val forkOn : Eff<'t, 'e, 'env> -> Eff<Fiber<'t, 'e>, 'e2, 'env>
```

### Derived Combinators

```fsharp
module Eff =
  /// Run two effects concurrently. The first to complete wins.
  /// The loser is aborted and its cleanup runs.
  val race    : Eff<'t, 'e, 'env> -> Eff<'t, 'e, 'env> -> Eff<'t, 'e, 'env>

  /// Run all effects concurrently. If any fails, the rest are aborted.
  /// Returns all results in order on success.
  val all     : Eff<'t, 'e, 'env> list -> Eff<'t list, 'e, 'env>

  /// Run an effect with a time limit. If the effect completes in time,
  /// returns Some. If time expires, the effect is aborted and returns None.
  val timeout : TimeSpan -> Eff<'t, 'e, 'env> -> Eff<'t option, 'e, 'env>
```

---

## How fork and forkOn Work

Both primitives:

1. Create a `CancellationTokenSource` for the child
2. Create a child `RuntimeStepper` with the same env and the new token
3. Build a child `TypedMachine` from the effect
4. Start `runTaskLoop` for the child machine
5. Register the child in the runner's fiber registry
6. Return a `Fiber<'t, 'e>` wrapping the child's Task + CTS

The difference is step 4:

- **`fork`**: `runTaskLoop childMachine token` -- a `task { }` that starts synchronously on the caller's thread. It steps through pure work immediately and yields at the first `MachineAwait` (async boundary). After yielding, the continuation resumes on whatever ThreadPool thread is available.

- **`forkOn`**: `Task.Run(fun () -> runTaskLoop childMachine token)` -- explicitly queues on the ThreadPool. The child starts on a ThreadPool thread from the beginning, never blocking the caller.

### When to use which

| Situation | Use |
|---|---|
| Network I/O, file I/O, async operations | `fork` -- yields quickly, minimal overhead |
| CPU-heavy computation | `forkOn` -- avoids blocking the caller |
| FFI / native interop that blocks | `forkOn` -- dedicated thread, doesn't starve async work |
| Most code | `fork` |

---

## Cancellation

### Mechanism

Each fiber owns a `CancellationTokenSource`. The token flows through the fiber's `RuntimeStepper`. Cancellation is checked at two points:

1. **Between steps in `stepEff`**: at the top of the stepping loop, `stepper.Token.IsCancellationRequested` is checked. If true, the current frames are unwound with `BoxedInterrupted`. Cleanup (defer/bracket) runs normally through the unwind path.

2. **During async awaits in `runTaskLoop`**: `taskObj.WaitAsync(token)` breaks out of a parked await when the token fires. The `OperationCanceledException` is caught, and since the token is cancelled, the cont function routes it as `BoxedInterrupted` instead of `BoxedExn`.

### Interruption is a 4th channel, not a defect

Interruption is intentional -- the caller asked the fiber to stop. It is not an error and not a defect. It gets its own exit channel:

```fsharp
// Internal:
type BoxedExit =
  | BoxedOk of ok: obj
  | BoxedErr of err: obj
  | BoxedExn of exn: exn
  | BoxedInterrupted          // new

// Public:
type Exit<'t, 'e> =
  | Ok of 't
  | Err of 'e
  | Exn of exn
  | Interrupted               // new
```

Frame handling for interruption:

| Frame | HandleInterrupted behavior |
|---|---|
| MapFrame | propagate -- no value to map |
| FlatMapFrame (bind) | propagate -- no value to bind |
| MapErrFrame | propagate -- not an error |
| FlatMapErrFrame (orElseWith) | propagate -- not an error |
| FlatMapExnFrame (catch) | **propagate -- not a defect, catch does NOT intercept** |
| DeferFrame (ensure) | **run cleanup, then propagate** |
| BracketReleaseFrame | **run release, then propagate** |
| BracketAcquireFrame | propagate -- acquisition didn't complete |
| DeferScopeFrame | propagate -- source didn't produce a value |

The critical property: **`Eff.catch` does not intercept interruption.** A user's `|> Eff.catch (fun _ -> fallback)` will not accidentally swallow an abort signal. Only cleanup frames (defer/bracket) execute during interruption, ensuring resources are released.

If cleanup itself fails during interruption, cleanup failure takes precedence (same rule as today for err/exn during cleanup).

### Fiber.abort semantics

```
1. fiber.CTS.Cancel()                  -- signal the child to stop
2. await fiber.Task                    -- wait for cleanup to finish
3. fiber.CTS.Dispose()                -- release CTS resources
4. return ()                           -- abort always succeeds
```

Abort waits for the child's Task to complete. This guarantees that cleanup (defer/bracket) has finished and resources have been released before the caller continues.

---

## Structured Concurrency: Runner as Scope

All fibers forked within a `runTask` invocation are tracked in a fiber registry. When the root effect completes, the runner aborts all outstanding fibers and waits for their cleanup.

```
runTask env program
  │
  ├─ root effect runs
  │   ├─ fork childA        ──► registered in registry
  │   ├─ fork childB        ──► registered in registry
  │   │   └─ fork grandchild ──► registered in same registry
  │   └─ root completes
  │
  ├─ abort all registered fibers
  ├─ await all cleanup
  └─ return root's Exit
```

No explicit scope parameter. No `Eff.scoped`. The runner IS the scope.

### Implementation

The registry is a `ConcurrentBag<FiberHandle>` stored in the `RuntimeStepper`. Child steppers (from `Fork`) share the same registry, so grandchildren register at the same level.

```fsharp
type FiberRegistry() =
  let fibers = ConcurrentBag<FiberHandle>()

  member _.Register(handle) = fibers.Add(handle)

  member _.AbortAll() = task {
    for handle in fibers do
      handle.CTS.Cancel()
    for handle in fibers do
      try let! _ = handle.Task in ()
      with _ -> ()
    for handle in fibers do
      handle.CTS.Dispose()
  }
```

---

## Runtime Changes

### Stepper interface

```fsharp
and Stepper<'env> =
  abstract Env: 'env
  abstract Token: CancellationToken                           // new
  abstract Registry: FiberRegistry                            // new
  abstract Project<'inner>: ('env -> 'inner) -> Stepper<'inner>
  abstract Fork: CancellationToken -> Stepper<'env>           // new
  abstract Step<'t, 'e>: Eff<'t, 'e, 'env> -> Frame<'env> list -> StepResult<'env>
  abstract Unwind: BoxedExit -> Frame<'env> list -> StepResult<'env>
```

- `Project` preserves the parent's token and registry (ProvideFrom child is the same fiber)
- `Fork` replaces the token (new fiber, independent cancellation) but shares the registry

### RuntimeStepper

```fsharp
type RuntimeStepper<'env>(env: 'env, token: CancellationToken, registry: FiberRegistry) as this =
  interface Stepper<'env> with
    member _.Env = env
    member _.Token = token
    member _.Registry = registry
    member _.Project(project) =
      RuntimeStepper<'inner>(project env, token, registry) :> Stepper<'inner>
    member _.Fork(childToken) =
      RuntimeStepper<'env>(env, childToken, registry) :> Stepper<'env>
    member _.Step inner frames = stepEff (this :> Stepper<'env>) inner frames
    member _.Unwind exit frames = unwind (this :> Stepper<'env>) exit frames
```

### stepEff: cancellation check between steps

```fsharp
let stepEff<'t, 'e, 'env> (stepper: Stepper<'env>) (eff: Eff<'t, 'e, 'env>) (frames: Frame<'env> list) =
  ...
  while not finished do
    if stepper.Token.IsCancellationRequested then
      result <- ValueSome(unwind stepper BoxedInterrupted currentFrames)
      finished <- true
    else
      match currentEff with
      | ...
      | Eff.Task tsk ->
        try
          let awaited = task { let! value = tsk () in return box value }
          result <-
            ValueSome(
              Await(
                awaited,
                function
                | Ok value ->
                  if stepper.Token.IsCancellationRequested then
                    unwind stepper BoxedInterrupted currentFrames
                  else
                    unwind stepper (BoxedOk value) currentFrames
                | Error ex ->
                  if stepper.Token.IsCancellationRequested then
                    unwind stepper BoxedInterrupted currentFrames
                  else
                    unwind stepper (BoxedExn ex) currentFrames
              )
            )
        ...
```

The Await continuation checks the token after the task completes. If the token is cancelled, the result is discarded and `BoxedInterrupted` is unwound instead, regardless of whether the task succeeded or failed.

### runTaskLoop: WaitAsync for responsive cancellation

```fsharp
let runTaskLoop (machine: Machine) (token: CancellationToken) : Task<BoxedExit> =
  task {
    ...
    while not finished do
      match current.Poll() with
      | MachineDone value -> ...
      | MachineAwait(taskObj, cont) ->
        try
          let! value = taskObj.WaitAsync(token)
          current <- cont (Ok value)
        with ex ->
          current <- cont (Error ex)
      | MachineSwitch(machine, resume) -> ...
  }
```

`WaitAsync(token)` breaks out of a parked await when the token is cancelled. The resulting exception flows through `cont (Error ex)`, where the cont function (in stepEff) checks the token and converts to `BoxedInterrupted`.

### unwind: handle BoxedInterrupted

```fsharp
let unwind (stepper: Stepper<'env>) (exit: BoxedExit) (frames: Frame<'env> list) =
  ...
  | frame :: rest ->
    let action =
      match currentExit with
      | BoxedOk value -> frame.HandleOk(value, rest)
      | BoxedErr err -> frame.HandleErr(err, rest)
      | BoxedExn ex -> frame.HandleExn(ex, rest)
      | BoxedInterrupted -> frame.HandleInterrupted(rest)     // new
    ...
```

### Frame: new HandleInterrupted member

```fsharp
and [<AbstractClass>] Frame<'env>() =
  abstract HandleOk: obj * Frame<'env> list -> UnwindAction<'env>
  abstract HandleErr: obj * Frame<'env> list -> UnwindAction<'env>
  abstract HandleExn: exn * Frame<'env> list -> UnwindAction<'env>
  abstract HandleInterrupted: Frame<'env> list -> UnwindAction<'env>   // new
```

All existing frames gain a `HandleInterrupted` that propagates:

```fsharp
// MapFrame, FlatMapFrame, MapErrFrame, FlatMapErrFrame,
// FlatMapExnFrame, BracketAcquireFrame, DeferScopeFrame:
override _.HandleInterrupted(rest) =
  ContinueWithExit(BoxedInterrupted, rest)

// DeferFrame (ensure) -- cleanup runs, then propagates:
override _.HandleInterrupted(rest) =
  RunCleanup(
    (BoxedEff<unit, 'e, 'env>(cleanup) :> BoxedEff<'env>),
    function
    | BoxedOk _ -> ContinueWithExit(BoxedInterrupted, rest)
    | BoxedErr cleanupErr -> ContinueWithExit(BoxedErr cleanupErr, rest)
    | BoxedExn cleanupExn -> ContinueWithExit(BoxedExn cleanupExn, rest)
    | BoxedInterrupted -> ContinueWithExit(BoxedInterrupted, rest)
  )

// BracketReleaseFrame -- release runs, then propagates:
override _.HandleInterrupted(rest) =
  runCleanup rest (function
    | BoxedOk _ -> ContinueWithExit(BoxedInterrupted, rest)
    | BoxedErr cleanupErr -> ContinueWithExit(BoxedErr cleanupErr, rest)
    | BoxedExn cleanupExn -> ContinueWithExit(BoxedExn cleanupExn, rest)
    | BoxedInterrupted -> ContinueWithExit(BoxedInterrupted, rest)
  )
```

### RunCleanup continuation: handle BoxedInterrupted

The anonymous cleanup frame in `unwind` also needs the new case:

```fsharp
| RunCleanup(cleanup, cont) ->
  let cleanupFrame =
    { new Frame<'env>() with
        member _.HandleOk(value, _) = cont (BoxedOk value)
        member _.HandleErr(err, _) = cont (BoxedErr err)
        member _.HandleExn(ex, _) = cont (BoxedExn ex)
        member _.HandleInterrupted(_) = cont BoxedInterrupted    // new
    }
  result <- ValueSome(Continue(cleanup, [ cleanupFrame ]))
```

### runTask: create registry, cleanup on exit

```fsharp
let runTask (env: 'env) (eff: Eff<'t, 'e, 'env>) : Task<Exit<'t, 'e>> =
  let registry = FiberRegistry()
  let stepper =
    RuntimeStepper<'env>(env, CancellationToken.None, registry) :> Stepper<'env>
  let machine =
    TypedMachine<'env>(stepper, stepper.Step eff []) :> Machine

  task {
    let! exit = runTaskLoop machine CancellationToken.None

    do! registry.AbortAll()

    match exit with
    | BoxedOk value -> return Exit.Ok(unbox<'t> value)
    | BoxedErr err -> return Exit.Err(unbox<'e> err)
    | BoxedExn ex -> return Exit.Exn ex
    | BoxedInterrupted -> return Exit.Interrupted
  }
```

---

## New Nodes

### Fork

```fsharp
type Fork<'t, 'e, 'e2, 'env>(body: Eff<'t, 'e, 'env>, useTaskRun: bool) =
  inherit Node<Fiber<'t, 'e>, 'e2, 'env>()

  interface INodeRuntime<'env> with
    member _.Enter(stepper, frames) =
      let cts = new CancellationTokenSource()
      let childStepper = stepper.Fork(cts.Token)
      let childMachine =
        TypedMachine<'env>(childStepper, childStepper.Step body []) :> Machine

      let childTask =
        if useTaskRun then
          Task.Run(fun () -> runTaskLoop childMachine cts.Token)
        else
          runTaskLoop childMachine cts.Token

      let handle = FiberHandle(childTask, cts)
      stepper.Registry.Register(handle)

      let fiber = Fiber<'t, 'e>(handle)
      unwind stepper (BoxedOk(box fiber)) frames
```

### AwaitFiber

Routes the child's exit through the parent's channels:

```fsharp
type AwaitFiber<'t, 'e, 'env>(fiber: Fiber<'t, 'e>) =
  inherit Node<'t, 'e, 'env>()

  interface INodeRuntime<'env> with
    member _.Enter(stepper, frames) =
      Await(
        task { let! exit = fiber.Handle.Task in return box exit },
        function
        | Ok boxedResult ->
          let exit = unbox<BoxedExit> boxedResult
          stepper.Unwind exit frames
        | Error ex ->
          stepper.Unwind (BoxedExn ex) frames
      )
```

Child `BoxedOk` becomes parent success. Child `BoxedErr` becomes parent managed error. Child `BoxedExn` becomes parent defect. Child `BoxedInterrupted` becomes parent interrupted.

### JoinFiber

Wraps the child's exit into an `Exit<'t, 'e>` value (always succeeds):

```fsharp
type JoinFiber<'t, 'e, 'e2, 'env>(fiber: Fiber<'t, 'e>) =
  inherit Node<Exit<'t, 'e>, 'e2, 'env>()

  interface INodeRuntime<'env> with
    member _.Enter(stepper, frames) =
      Await(
        task { let! exit = fiber.Handle.Task in return box exit },
        function
        | Ok boxedResult ->
          let exit = unbox<BoxedExit> boxedResult
          let typedExit =
            match exit with
            | BoxedOk v -> Exit.Ok(unbox<'t> v)
            | BoxedErr e -> Exit.Err(unbox<'e> e)
            | BoxedExn ex -> Exit.Exn ex
            | BoxedInterrupted -> Exit.Interrupted
          stepper.Unwind (BoxedOk(box typedExit)) frames
        | Error ex ->
          stepper.Unwind (BoxedExn ex) frames
      )
```

### AbortFiber

```fsharp
type AbortFiber<'t, 'e, 'e2, 'env>(fiber: Fiber<'t, 'e>) =
  inherit Node<unit, 'e2, 'env>()

  interface INodeRuntime<'env> with
    member _.Enter(stepper, frames) =
      fiber.Handle.CTS.Cancel()
      Await(
        task {
          try let! _ = fiber.Handle.Task in ()
          with _ -> ()
          fiber.Handle.CTS.Dispose()
          return box ()
        },
        function
        | Ok _ -> stepper.Unwind (BoxedOk(box ())) frames
        | Error _ -> stepper.Unwind (BoxedOk(box ())) frames
      )
```

---

## Derived Combinator Implementations

### race

Fork both effects, `Task.WhenAny` to find the winner, abort the loser, route the winner's exit:

```fsharp
let race (a: Eff<'t, 'e, 'env>) (b: Eff<'t, 'e, 'env>) : Eff<'t, 'e, 'env> =
  Eff.Node(RaceNode(a, b))
```

Internally, the `RaceNode`'s Enter:
1. Creates two child steppers with independent CTS's
2. Starts both as `runTaskLoop` tasks
3. Returns `Await(Task.WhenAny(taskA, taskB), ...)`
4. In the continuation: cancel the loser, await its cleanup, route the winner's `BoxedExit` through `stepper.Unwind`

### all

Fork all effects, await all completions (or first failure):

1. Start all as child tasks
2. Await tasks one by one (or use a completion loop)
3. On first failure: cancel remaining, await cleanup, route the error
4. On all success: collect results, route as `BoxedOk(box resultList)`

### timeout

Fork effect + `Task.Delay`, race between them:

1. Start effect as child task
2. Create `Task.Delay(duration)`
3. `Task.WhenAny(effectTask, delayTask)`
4. If delay wins: cancel effect, await cleanup, return `None`
5. If effect wins: return `Some result`

---

## Performance Impact

### Non-forked effects (existing code, no fork calls)

| Change | Cost |
|---|---|
| `RuntimeStepper` constructor takes `CancellationToken` + `FiberRegistry` | 0 -- stored as fields |
| `stepEff` checks `stepper.Token.IsCancellationRequested` each step | ~0 -- `CancellationToken.None` is never cancelled, branch predictor always correct, ~0.3ns |
| `runTaskLoop` calls `taskObj.WaitAsync(CancellationToken.None)` | 0 -- .NET fast-paths this, returns the original task, no allocation |
| `Stepper` interface gains `Token`, `Registry`, `Fork` members | 0 -- vtable grows, no cost unless called |
| `Frame` gains `HandleInterrupted` abstract member | 0 -- vtable grows, no cost unless called |
| `unwind` has one more match arm | ~0 -- `BoxedInterrupted` never occurs without fork |
| `FiberRegistry` created in `runTask` | ~1 allocation (~40 bytes), empty bag |
| `registry.AbortAll()` called on exit with empty registry | ~0 -- iterates nothing |

**Total overhead on existing non-concurrent code: effectively zero.** The branch predictor handles the cancellation check. WaitAsync fast-paths on `CancellationToken.None`. The empty registry allocation is negligible.

### Per-fiber overhead

| Item | Bytes |
|---|---|
| `CancellationTokenSource` | ~80 |
| `Task` async state machine (from `runTaskLoop task { }`) | ~200 |
| `RuntimeStepper` (env ref + token + registry ref) | ~40 |
| `TypedMachine` + initial `StepResult` | ~60 |
| `Fiber<'t, 'e>` + `FiberHandle` | ~40 |
| **Total per fiber** | **~420 bytes** |

At scale:

| Fiber count | Memory |
|---|---|
| 1,000 | ~420 KB |
| 10,000 | ~4.2 MB |
| 100,000 | ~42 MB |
| 1,000,000 | ~420 MB |

For context: ASP.NET Core handles 100K+ concurrent requests with similar per-request overhead. A 1M-connection server at 420 MB for fiber state is within reason for a dedicated server process.

### fork vs forkOn

| | fork | forkOn |
|---|---|---|
| Startup cost | Machine + CTS allocation | Machine + CTS allocation + ThreadPool queue |
| First steps | Synchronous on caller's thread until first await | Runs on ThreadPool thread from start |
| Caller blocking | Briefly, until child yields | Never |
| Best for | I/O-bound work (yields quickly) | CPU-bound / blocking work |

### WaitAsync overhead on forked fibers

`taskObj.WaitAsync(token)` where token is cancellable (forked fibers):
- Task already completed: fast path, returns immediately, no allocation
- Task pending: allocates a wrapper task (~80 bytes per in-flight await)
- This only applies to forked fibers -- the root effect uses `CancellationToken.None` (zero overhead)

### Cancellation cost

| Operation | Cost |
|---|---|
| `Fiber.abort` -> `cts.Cancel()` | O(1), signals the token |
| Cancellation check in stepEff | ~0.3ns per step, branch predicted |
| WaitAsync wakeup on cancellation | One task completion callback |
| Cleanup unwind | Proportional to number of defer/bracket frames |

### ConcurrentBag overhead (fiber registry)

| Operation | Cost |
|---|---|
| `Register` (per fork) | One lock-free append, ~10-20ns |
| `AbortAll` (per runTask exit) | Iterates all fibers, cancels each, awaits each |

The registry is a flat bag -- no hierarchy, no tree traversal. All fibers (children, grandchildren) register at the same level.

---

## Env Safety

`fork` starts the child's `task { }` on the caller's thread. It runs synchronously until the first await. After that, the continuation runs on a ThreadPool thread. Multiple forked fibers may execute simultaneously on different threads.

This means **forked fibers access the env concurrently.** The env must be safe for concurrent reads.

In the typical EffSharp pattern, envs are bags of stateless capability providers:

```fsharp
type AppEnv() =
  interface Effect.Stdio with
    member _.Stdio = Stdio.Provider()
  interface Effect.Clock with
    member _.Clock = Clock.Provider()
```

These are safe for concurrent access -- the interface implementations return stateless objects. This is the expected pattern.

If a user puts mutable state in the env, concurrent access is their responsibility, same as any .NET concurrent programming with `Task.Run`.

---

## Example Usage

### Fork and await

```fsharp
let program () = eff {
  let! fiberA = Eff.fork (fetchUser userId)
  let! fiberB = Eff.fork (fetchPosts userId)
  let! user  = Fiber.await fiberA
  let! posts = Fiber.await fiberB
  return user, posts
}
```

### Race

```fsharp
let fetchFromFastest url = eff {
  let! response = Eff.race (fetchFromCdn url) (fetchFromOrigin url)
  return response
}
```

### Timeout

```fsharp
let fetchWithTimeout url = eff {
  let! result = Eff.timeout (TimeSpan.FromSeconds 5.0) (Http.get url)
  match result with
  | Some response -> return response
  | None -> return! Err (TimedOut url)
}
```

### Server accept loop

```fsharp
let server () = eff {
  do! Net.withListener (addr 8080) 128 (fun listener -> eff {
    while true do
      let! stream, remote = Net.tcpAccept listener
      let! _ = Eff.fork (handleConnection stream remote)
      ()
  })
}
// When the listener closes (or the runner exits),
// all forked connection handlers are aborted + cleaned up.
```

### CPU-bound work on ThreadPool

```fsharp
let program () = eff {
  let! fiber = Eff.forkOn (eff {
    return expensiveComputation data
  })
  // caller continues immediately, not blocked
  do! otherWork ()
  let! result = Fiber.await fiber
  return result
}
```
