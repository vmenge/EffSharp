namespace EffSharp.Gen

open System

module Naming =
  let private stripInterfacePrefix (name: string) =
    if name.Length > 1 && name[0] = 'I' && Char.IsUpper(name[1]) then
      name.Substring(1)
    else
      name

  let wrappedTypeName serviceName = stripInterfacePrefix serviceName

  let wrappedEnvironmentName serviceName = $"Effect.{wrappedTypeName serviceName}"

  let environmentName mode serviceName =
    if mode = Mode.Wrap then
      wrappedEnvironmentName serviceName
    else
      serviceName

  let propertyName serviceName = wrappedTypeName serviceName

  let wrapperName (memberName: string) = memberName
