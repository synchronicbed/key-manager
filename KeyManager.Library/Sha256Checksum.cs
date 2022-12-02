﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Leosac.KeyManager.Library
{
    public class Sha256Checksum : KeyChecksum
    {
        public override string Name => "SHA256";

        public override byte[] ComputeKCV(Key key, byte[]? iv = null)
        {
            using (var hash = SHA256.Create())
            {
                var rawkey = key.GetAggregatedValue<byte[]>(KeyValueFormat.Binary);
                // Use the IV as a Salt
                byte[] data;
                if (iv != null)
                {
                    data = new byte[iv.Length + rawkey.Length];
                    Array.Copy(iv, 0, data, 0, iv.Length);
                    Array.Copy(rawkey, 0, data, iv.Length, rawkey.Length);
                }
                else
                {
                    data = rawkey;
                }
                return hash.ComputeHash(data);
            }
        }
    }
}
