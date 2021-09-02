using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Jape
{
	public static partial class Serializer
    {
        public static partial class Dynamic
        {
            public class Writer
            {
                private List<byte> bytes = new List<byte>();

                public void Write(byte data)
                {
                    bytes.Add(data);
                }

                public void Write(IEnumerable<byte> data)
                {
                    bytes.AddRange(data);
                }

                public byte[] ToArray()
                {
                    return bytes.ToArray();
                }

                public static bool CanCatch(Writer writer) => writer.bytes.Count == 0;
                public static void OnRelease(Writer writer) => writer.bytes.Clear();
            }
        }
    }
}