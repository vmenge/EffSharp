namespace EffSharp.Std

open System

module String =
  let isNullOrEmpty (str: string) = String.IsNullOrEmpty str
  let isNullOrWhiteSpace (str: string) = String.IsNullOrWhiteSpace str
  let len (str: string) = str.Length

  let substring (start: int) (len: int) (str: string) : string option =
    try
      str.Substring(start, len) |> Some
    with _ ->
      None

  let substringFrom (start: int) (str: string) : string option =
    try
      str.Substring(start) |> Some
    with _ ->
      None

  let splitBy (separators: char array) (str: string) = str.Split(separators)

  let splitOnce (separator: string) (str: string) : (string * string) option =
    let i = str.IndexOf(separator)

    if i < 0 then
      None
    else
      let left = str.Substring(0, i)
      let right = str.Substring(i + separator.Length)

      Some(left, right)

  let joinWith (separator: char) (values: string seq) =
    String.Join(separator, values)

  let joinWithString (separator: string) (values: string seq) =
    String.Join(separator, values)

  let startsWith (value: string) (str: string) = str.StartsWith(value)
