using System.Runtime.CompilerServices;

namespace System7.Avalonia.Controls;

/// <summary>Keeps one behavior per owner while the owner turns it on, and disposes it when the owner turns it off.</summary>
internal sealed class System7Attachments<TOwner, TBehavior>(Func<TOwner, TBehavior> create)
    where TOwner : class
    where TBehavior : class, IDisposable
{
    private readonly ConditionalWeakTable<TOwner, TBehavior> instances = new();

    public void Set(TOwner owner, bool enabled)
    {
        if (enabled) instances.GetValue(owner, key => create(key));
        else if (instances.TryGetValue(owner, out var behavior))
        {
            instances.Remove(owner);
            behavior.Dispose();
        }
    }

    public bool TryGet(TOwner owner, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out TBehavior behavior) => instances.TryGetValue(owner, out behavior);
}
