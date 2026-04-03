# F# Eff Effect Spec

## Goals

Build an `Eff` effect for F# with these priorities:

- good external developer ergonomics
- strong performance
- support for sync and async effects
- support for environment-based dependency injection
- lazy effect construction
- unified error handling
- small composable environments

Internal implementation ergonomics are less important than runtime behavior and external API quality.

---

## Core Representation

Primary shape:

```fsharp
[<Struct>]
type Eff<'t, 'env> =
  private
  | Pure of value: 't
  | Err of err: exn
  | Delay of delay: (unit -> Eff<'t, 'env>)
  | Thunk of thunk: (unit -> 't)
  | Tsk of tsk: (unit -> Task<'t>)
  | Asy of asy: (unit -> Async<'t>)
  | Pending of pending: obj * step: Step<'t, 'env>
```

Notes:

- `Eff` is a struct discriminated union.
- `Pure` is the zero-cost success fast path.
- `Err` stores `exn` directly.
- `Delay`, `Thunk`, `Tsk`, and `Asy` are direct lazy runtime cases.
- `Pending` stores erased composed runtime state plus a private step delegate.
- the union is private; construction is intended to go through the `Eff` module
- No extra public branch for “early exit”.

### Why `exn`

Chosen over a custom internal error union because:

- simpler runtime
- less branching
- better interop with .NET exceptions, `Task`, `Async`
- preserves stack traces when the original exception is kept
- lower implementation complexity

`Result` and `Option` inputs are normalized into this error channel.

---

## Error Handling

### Internal model

All failures normalize to:

```fsharp
Err of exn
```

Examples:

- thrown exception -> `Err ex`
- faulted `Task` -> `Err ex`
- faulted `Async` -> `Err ex`
- `Result.Error e` -> mapped to an exception
- `None` -> mapped to an exception

### Stack traces

Using `exn` preserves stack traces as long as the original exception object is kept.

If an error source is not originally an exception, such as `Option.None` or `Result.Error`, any generated exception only has a stack trace from the point where it was created.

### CE exception behavior

The `eff {}` computation expression should automatically catch exceptions thrown inside user callbacks and normalize them into `Err exn`.

This applies to:

- pure synchronous user code inside the CE
- `bind`
- `map`
- `catch`
- conversions from `Task`, `Async`, Promise
- cleanup/finalizer code as appropriate

Exceptions should not leak through the effect abstraction by default.

---

## Laziness

Effects should be lazy at effect boundaries by default.

Meaning:

- constructing an `Eff` value should not start work
- running happens only via `run*` functions
- interop constructors from eager worlds should take thunks
- `Pure` and `Err` remain strict terminal values
- laziness should primarily live in suspended/effectful constructors and pending state

Preferred constructor shapes:

```fsharp
Eff.value     : 'T -> Eff<'T, 'Env>
Eff.err       : exn -> Eff<'T, 'Env>
Eff.errwith   : string -> Eff<'T, 'Env>
Eff.thunk     : (unit -> 'T) -> Eff<'T, 'Env>
Eff.delay     : (unit -> Eff<'T, 'Env>) -> Eff<'T, 'Env>
Eff.ofTask    : (unit -> Task<'T>) -> Eff<'T, 'Env>
Eff.ofValueTask : (unit -> ValueTask<'T>) -> Eff<'T, 'Env>
Eff.ofAsync   : (unit -> Async<'T>) -> Eff<'T, 'Env>
```

Benefits:

- referential transparency
- better retry/finalizer semantics
- better composability
- easier guard/ensure-style APIs
- clearer control over when effects begin

Performance note:

- laziness is not automatically faster
- main cost comes from closures/thunks and extra nodes
- `Pure` and `Err` should remain strict and cheap

---

## Runtime Representation

The current implementation uses specialized direct DU cases for common suspended forms and reserves `Pending` for composed/internal runtime state.

```fsharp
[<Struct>]
type Eff<'t, 'env> =
  private
  | Pure of value: 't
  | Err of err: exn
  | Delay of delay: (unit -> Eff<'t, 'env>)
  | Thunk of thunk: (unit -> 't)
  | Tsk of tsk: (unit -> Task<'t>)
  | Asy of asy: (unit -> Async<'t>)
  | Pending of pending: obj * step: Step<'t, 'env>
```

### `Step`

`Step<'T, 'Env>` is a private delegate used to advance composed pending state.

Conceptual shape:

```fsharp
type Step<'T, 'Env> =
    delegate of obj * 'Env -> ValueTask<Eff<'T, 'Env>>
```

This is the preferred mental model:

- `Pure` and `Err` are terminal
- `Delay`, `Thunk`, `Tsk`, and `Asy` are direct lazy leaf cases
- `Pending (state, step)` means the runtime should ask `step` to advance `state`
- `Pending` is intended for composed operations such as `map`, `bind`, `catch`, and `finally`

### Why not make everything `Pending`

Using dedicated DU cases for common suspended forms avoids paying an extra wrapper allocation for every pending computation.

This is especially important for:

- delayed sync work
- task interop
- async interop
- other simple lazy leaf effects

`Pending` still exists for the cases that genuinely need erased composed state.

## Why not `Pending of ValueTask<Eff<'T, 'Env>>`

Rejected as the primary public representation.

Example rejected shape:

```fsharp
[<Struct>]
type Eff<'T, 'Env> =
  | Pure of 'T
  | Err of exn
  | Pending of ValueTask<Eff<'T, 'Env>>
```

Reasons:

- `ValueTask` has one-shot/sensitive consumption semantics
- storing it directly in a copyable struct union is fragile
- copying `Eff` copies the `ValueTask`
- makes the public `Eff` type fatter than desired
- couples public representation too tightly to a low-level async primitive

`ValueTask` may still be used internally inside the runtime, but the current implementation normalizes `ofValueTask` into the `Tsk` path.

---

## Running Eff

### Public runners

On .NET:

```fsharp
Eff.runSync  : 'Env -> Eff<'T, 'Env> -> Result<'T, exn>
Eff.runTask  : 'Env -> Eff<'T, 'Env> -> Task<Result<'T, exn>>
Eff.runAsync : 'Env -> Eff<'T, 'Env> -> Async<Result<'T, exn>>
```

On Fable:

```fsharp
Eff.runPromise : 'Env -> Eff<'T, 'Env> -> JS.Promise<Result<'T, exn>>
```

### `runSync`

`runSync` is valid and useful.

It is especially appropriate at top-level boundaries such as:

- process entrypoints
- CLI tools
- test runners
- worker entrypoints
- outermost host boundaries under your control

It should not be the only runner, because:

- request-level/server-internal code often should stay async
- UI code should not block
- JS/Fable cannot provide the same normal blocking semantics

---

## Environment / Dependency Injection

Environment support is desired and should be composable.

Chosen shape:

```fsharp
type Eff<'T, 'Env>
```

### Why `'T` first and `'Env` second

This reads better in the common case and allows nicer env-less aliases.

Inference usually leaves `'Env` generic if unconstrained.

Example:

```fsharp
Eff.value 123
```

should infer something equivalent to:

```fsharp
Eff<int, 'Env>
```

not force `unit`.

### No default generic parameter

F# does not provide the desired default generic argument behavior here, so aliases should be used where helpful.

Possible alias pattern:

```fsharp
type Eff0<'T> = Eff<'T, unit>
```

---

## Dependency Injection Style

Large monolithic env records were rejected as the primary model because ergonomics become poor.

Two candidate DI styles were discussed.

### Preferred style: capability interfaces

Define small capabilities:

```fsharp
type IHasLogger =
    abstract Logger : ILogger

type IHasDb =
    abstract Db : IDb
```

Then functions depend only on what they need:

```fsharp
val logInfo  : string -> Eff<unit, 'Env> when 'Env :> IHasLogger
val getUser  : int -> Eff<User, 'Env> when 'Env :> IHasDb
val greetUser : int -> Eff<string, 'Env> when 'Env :> IHasLogger and 'Env :> IHasDb
```

Concrete env values implement multiple interfaces.

This gives:

- small env requirements
- decent composition
- no giant record in every function signature

### Secondary style: small records + adapters

Also possible:

```fsharp
type LoggerEnv = { Logger : ILogger }
type DbEnv = { Db : IDb }
```

Then use adapter functions or `local` to run smaller-env effects inside bigger-env effects.

This is more explicit but more boilerplate-heavy.

### Chosen preference

Capability interfaces are preferred over small-record adapters for primary ergonomics.

---

## Computation Expression Semantics

The CE should support `let!` over multiple source types by converting them into `Eff`.

Supported conceptual sources:

- `Eff<'T, 'Env>`
- `Result<'T, 'E>`
- `Option<'T>`
- `Task<'T>`
- `ValueTask<'T>`
- `Async<'T>`
- Promise under Fable

This can be implemented with `Source` overloads or overloaded `Bind`, with a preference for normalizing sources into `Eff` first.

Example target usage:

```fsharp
eff {
  let! x = someIo
  let! y = someResult
  let! z = someOption
  let! t = someTask
  return x
}
```

### Source normalization

Preferred strategy:

- `Result` -> immediate `Pure` / `Err`
- `Option` -> immediate `Pure` / `Err`
- `Task` -> lazy `Tsk`
- `Async` -> lazy `Asy`
- `ValueTask` -> currently normalized into the `Tsk` path
- no storing of `Result` or `Option` inside `Pending`

---

## Guard / Ensure / Early Return

No extra public union case should be added for early exit.

Rejected idea:

- adding a dedicated `Exit` branch to `Eff`

Preferred direction:

- use helper combinators that preserve the current value or short-circuit through existing semantics

The most practical API shape is value-preserving helpers such as:

```fsharp
Eff.ensure : ('T -> bool) -> Eff<'T, 'Env> -> Eff<'T, 'Env>
```

Example:

```fsharp
loadUser id
|> Eff.ensure isActive
```

Inside CE:

```fsharp
eff {
  let! user = loadUser id |> Eff.ensure isActive
  return user
}
```

This preserves the successful value and stops the computation if the predicate fails.

The exact internal encoding of this short-circuit remains open, but no extra public DU branch should be added.

---

## Cleanup / Defer

Defer-style cleanup is desired, similar in spirit to Go/Odin `defer`.

Desired capabilities:

```fsharp
Eff.defer      : (unit -> unit) -> Eff<unit, 'Env>
Eff.deferIO    : (unit -> Eff<unit, 'Env>) -> Eff<unit, 'Env>
Eff.finallyDo  : (unit -> unit) -> Eff<'T, 'Env> -> Eff<'T, 'Env>
Eff.finallyIO  : (unit -> Eff<unit, 'Env>) -> Eff<'T, 'Env> -> Eff<'T, 'Env>
Eff.bracket    : Eff<'R, 'Env> -> ('R -> Eff<unit, 'Env>) -> ('R -> Eff<'T, 'Env>) -> Eff<'T, 'Env>
```

Semantics:

- cleanup runs on success
- cleanup runs on failure
- multiple defers run in LIFO order
- cleanup should also run when computations short-circuit

---

## Public API Direction

### Core constructors

```fsharp
Eff.value     : 'T -> Eff<'T, 'Env>
Eff.err       : exn -> Eff<'T, 'Env>
Eff.errwith   : string -> Eff<'T, 'Env>
Eff.thunk     : (unit -> 'T) -> Eff<'T, 'Env>
Eff.delay     : (unit -> Eff<'T, 'Env>) -> Eff<'T, 'Env>
Eff.ofTask    : (unit -> Task<'T>) -> Eff<'T, 'Env>
Eff.ofValueTask : (unit -> ValueTask<'T>) -> Eff<'T, 'Env>
Eff.ofAsync   : (unit -> Async<'T>) -> Eff<'T, 'Env>
```

### Core combinators

```fsharp
Eff.map       : Eff<'T, 'Env> -> ('T -> 'U) -> Eff<'U, 'Env>
Eff.bind      : Eff<'T, 'Env> -> ('T -> Eff<'U, 'Env>) -> Eff<'U, 'Env>
Eff.catch     : Eff<'T, 'Env> -> (exn -> Eff<'T, 'Env>) -> Eff<'T, 'Env>
Eff.mapError  : Eff<'T, 'Env> -> (exn -> exn) -> Eff<'T, 'Env>
Eff.ensure    : Eff<'T, 'Env> -> ('T -> bool) -> Eff<'T, 'Env>
```

### Environment helpers

Exact names remain open, but conceptually:

```fsharp
Eff.ask       : Eff<'Env, 'Env>
Eff.service   : Eff<'Service, 'Env> when 'Env :> IHasServiceLike
Eff.provide   : 'Env -> Eff<'T, 'Env> -> Eff<'T, unit>   // or direct runner-oriented variant
Eff.local     : ('OuterEnv -> 'InnerEnv) -> Eff<'T, 'InnerEnv> -> Eff<'T, 'OuterEnv>
```

---

## Performance Priorities

Primary performance principles:

- `Pure` path must be as cheap as possible
- `Err` path must stay simple
- direct lazy leaf cases should avoid extra wrapper allocations
- `Pending` should allocate only when composition genuinely needs erased state
- avoid large internal error unions
- prefer normalization of source types early
- do not expose `ValueTask` directly as the public pending representation
- avoid unnecessary closures/thunks in hot paths
- laziness should exist at the effect boundary, not as pointless overhead everywhere

### Hot-path guidance

`Eff` is intended for orchestration and effect boundaries, not for tight inner loops.

- keep simulation, numeric, and per-frame hot-path logic as plain F# functions
- embed those pure functions inside an upstream `Eff` workflow
- avoid building heap-backed pending chains in performance-critical inner loops

### Main expected costs

The likely hotspots are:

- closure allocation
- boxed state for composed `Pending` work
- task/async/promise interop
- async state machine churn
- delegate invocation in pending steps
- large/copy-heavy value types

Branching on the specialized DU cases is acceptable and part of the chosen design.

---

## Open Questions

These were left intentionally open or only partially decided:

1. Exact internal encoding of short-circuit behavior for `ensure`/guard-like helpers
   - public API direction is chosen
   - internal control mechanism remains open

2. Exact promise interop API on Fable

3. Whether to expose both `runTask` and `runValueTask`
   - `runTask` is clearly desired
   - `runValueTask` remains optional/advanced

---

## Summary of Chosen Decisions

Chosen:

- `Eff<'T, 'Env>`
- private struct DU
- struct DU
- direct DU cases for `Delay`, `Thunk`, `Tsk`, and `Asy`
- `Pending of obj * Step` for composed runtime state
- errors represented as `exn`
- effects lazy by default
- laziness should live at effect boundaries, while terminal cases stay strict
- `Task`/`Async` constructors should take thunks
- `ValueTask` is accepted at the API boundary and currently normalized into the task path
- no extra public `Exit` branch
- DI via small capability interfaces is preferred
- multiple source types can participate in `let!` by normalization into `Eff`
- `runSync` is valid at top-level boundaries
- defer/finally/bracket-style cleanup should exist

Rejected:

- large internal error union as the primary model
- giant monolithic env as the only DI story
- public `Pending of ValueTask<Eff<...>>`
- extra public DU branch just for early exit
