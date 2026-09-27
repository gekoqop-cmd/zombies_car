using System;

namespace ZombiCar.Core
{
    /// <summary>A serialized reference that may stay empty on purpose (e.g. a locked map card has no maps); the
    /// build's scene validator does not report it.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalReferenceAttribute : Attribute
    {
    }
}
