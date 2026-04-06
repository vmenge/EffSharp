namespace EffFs.EffectGen

open System

[<AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)>]
type EffectAttribute() =
  inherit Attribute()
