namespace EffSharp.Std

module Task =
  let map f t = task {
    let! t = t
    return f t
  }

  let bind f t = task {
    let! t = t
    return! f t
  }
