namespace EffSharp.Std

module Std =
  type StdProvider internal () =
    interface Effect.Fs with
      member _.Fs = Fs.Provider()

    interface Effect.Stdio with
      member _.Stdio = Stdio.Provider()

    interface Effect.Clock with
      member _.Clock = Clock.Provider()

    interface Effect.Env with
      member _.Env = Env.Provider()

    interface Effect.Random with
      member _.Random = Random.Provider()

    interface Effect.Net with
      member _.Net = Net.Provider()

    interface Effect.Command with
      member _.Command = Command.Provider()

  let Provider () = StdProvider()
