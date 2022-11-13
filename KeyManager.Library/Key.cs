﻿using Leosac.KeyManager.Library.KeyStore;
using Leosac.KeyManager.Library.Policy;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace Leosac.KeyManager.Library
{
    public class Key : KMObject
    {
        public Key()
        {
            _value = String.Empty;
            Policies = new ObservableCollection<IKeyPolicy>();
            _link = new KeyLink();
        }

        public Key(KeyTag tags, uint keySize)
        {
            _tags = tags;
            _keySize = keySize;
            _value = String.Empty;
            Policies = new ObservableCollection<IKeyPolicy>
            {
                new KeyLengthPolicy(keySize)
            };
            _link = new KeyLink();
        }

        private string _value;

        public string Value
        {
            get => _value;
            set
            {
                ValidatePolicies(value);
                SetProperty(ref _value, value);
            }
        }

        private KeyTag _tags;

        public KeyTag Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        private uint _keySize;

        public uint KeySize
        {
            get => _keySize;
            set => SetProperty(ref _keySize, value);
        }

        public ObservableCollection<IKeyPolicy> Policies { get; set; }

        private KeyLink _link;

        public KeyLink Link
        {
            get => _link;
            set => SetProperty(ref _link, value);
        }

        public void ValidatePolicies()
        {
            ValidatePolicies(this.Value);
        }

        public void ValidatePolicies(string key)
        {
            foreach (var policy in Policies)
            {
                policy.Validate(key);
            }
        }
    }
}