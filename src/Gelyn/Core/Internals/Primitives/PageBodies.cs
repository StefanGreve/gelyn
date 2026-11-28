using System.Buffers;
using System.Runtime.InteropServices;

namespace Gelyn.Core.Internals.Primitives;

/// <summary>
///     Owns the HTML bodies of one build's pages, which it keeps in native memory.
/// </summary>
/// <remarks>
///     A body lives from the render of its page to the write, long enough to be promoted to generation 2 or to start
///     there on the large object heap. In native memory, it costs the garbage collector nothing.
/// </remarks>
internal sealed class PageBodies : IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<NativeBody> _bodies = [];
    private bool _disposed;

    /// <summary>
    ///     Allocates a body.
    /// </summary>
    /// <param name="length">
    ///     The length of the body in bytes.
    /// </param>
    /// <returns>
    ///     The body, whose contents are undefined until written, or an empty block when <paramref name="length"/> is
    ///     zero.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="length"/> is negative.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///     The owner has been disposed.
    /// </exception>
    public Memory<byte> Allocate(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (length == 0) return Memory<byte>.Empty;

        // The render threads allocate at the same time.
        lock (this._lock)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);

            var body = new NativeBody(this, length);
            this._bodies.Add(body);

            return body.Memory;
        }
    }

    /// <summary>
    ///     Frees a body that this owner allocated, after which a read of it throws
    ///     <see cref="ObjectDisposedException"/>.
    /// </summary>
    /// <param name="body">
    ///     The body to free. Memory that this owner did not allocate, and a body that is already freed, are left alone.
    /// </param>
    public void Release(ReadOnlyMemory<byte> body)
    {
        if (MemoryMarshal.TryGetMemoryManager(body, out NativeBody? native) && native.Owner == this)
            native.Free();
    }

    /// <summary>
    ///     Frees every body that has not been released.
    /// </summary>
    public void Dispose()
    {
        lock (this._lock)
        {
            foreach (NativeBody body in this._bodies)
                body.Free();

            this._bodies.Clear();
            this._disposed = true;
        }
    }

    // Without a finalizer, as CA2015 requires, since a span over the memory can outlive the object that handed it out.
    private sealed unsafe class NativeBody : MemoryManager<byte>
    {
        private readonly int _length;
        private byte* _pointer;

        public NativeBody(PageBodies owner, int length)
        {
            this.Owner = owner;
            this._length = length;
            this._pointer = (byte*)NativeMemory.Alloc((nuint)length);
        }

        public PageBodies Owner { get; }

        private byte* Pointer
        {
            get
            {
                ObjectDisposedException.ThrowIf(this._pointer == null, this);

                return this._pointer;
            }
        }

        public override Span<byte> GetSpan() => new(this.Pointer, this._length);

        public override MemoryHandle Pin(int elementIndex = 0) => new(this.Pointer + elementIndex);

        public override void Unpin()
        {
        }

        public void Free()
        {
            if (this._pointer == null) return;

            NativeMemory.Free(this._pointer);
            this._pointer = null;
        }

        protected override void Dispose(bool disposing) => this.Free();
    }
}
