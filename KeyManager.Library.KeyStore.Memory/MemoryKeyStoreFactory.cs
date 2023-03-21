﻿using Leosac.KeyManager.Library.Plugin;
using Newtonsoft.Json;

namespace Leosac.KeyManager.Library.KeyStore.Memory.UI
{
    public class MemoryKeyStoreFactory : KeyStoreFactory
    {
        public override string Name => "Memory Key Store";

        public override KeyStore CreateKeyStore()
        {
            return new MemoryKeyStore();
        }

        public override Type GetPropertiesType()
        {
            return typeof(MemoryKeyStoreProperties);
        }

        public override KeyStoreProperties CreateKeyStoreProperties()
        {
            return new MemoryKeyStoreProperties();
        }

        public override KeyStoreProperties? CreateKeyStoreProperties(string serialized)
        {
            return JsonConvert.DeserializeObject<MemoryKeyStoreProperties>(serialized);
        }
    }
}
