/*----------------------------------------------------------------
// Copyright (C) 2015 广州，Lucky Game
//
// 模块名：
// 创建者：D.S.Qiu
// 修改者列表：
// 创建日期：April 02 2016
// 模块描述：
//----------------------------------------------------------------*/
using System;
using System.IO;
//Unity用的是.Net2.0,但是也有Unity或Mono对一部分特性或api有支持
/// <summary>
/// Extension methods for streams.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// Reads all the bytes from the current stream and writes them to the destination stream.
    /// </summary>
    /// <param name="original">The current stream.</param>
    /// <param name="destination">The stream that will contain the contents of the current stream.</param>
    /// <exception cref="System.ArgumentNullException">Destination is null.</exception>
    /// <exception cref="System.NotSupportedException">The current stream does not support reading.-or-destination does not support Writing.</exception>
    /// <exception cref="System.ObjectDisposedException">Either the current stream or destination were closed before the System.IO.Stream.CopyTo(System.IO.Stream) method was called.</exception>
    /// <exception cref="System.IO.IOException">An I/O error occurred.</exception>
    public static void CopyTo(this Stream original, Stream destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException("destination");
        }
        if (!original.CanRead && !original.CanWrite)
        {
            throw new ObjectDisposedException("ObjectDisposedException");
        }
        if (!destination.CanRead && !destination.CanWrite)
        {
            throw new ObjectDisposedException("ObjectDisposedException");
        }
        if (!original.CanRead)
        {
            throw new NotSupportedException("NotSupportedException source");
        }
        if (!destination.CanWrite)
        {
            throw new NotSupportedException("NotSupportedException destination");
        }

        byte[] array = new byte[4096];
        int count;
        while ((count = original.Read(array, 0, array.Length)) != 0)
        {
            destination.Write(array, 0, count);
        }
    }
}

