namespace EffSharp.Std

module Result =
  let tryCatch f =
    try
      Ok(f ())
    with exn ->
      Error exn
