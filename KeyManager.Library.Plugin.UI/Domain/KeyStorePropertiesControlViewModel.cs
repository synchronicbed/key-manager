﻿using CommunityToolkit.Mvvm.ComponentModel;
using Leosac.KeyManager.Library.KeyStore;

namespace Leosac.KeyManager.Library.Plugin.UI.Domain
{
    public abstract class KeyStorePropertiesControlViewModel : ObservableValidator
    {
        protected KeyStoreProperties? _properties;

        public KeyStoreProperties? Properties
        {
            get => _properties;
            set => SetProperty(ref _properties, value);
        }
    }
}
