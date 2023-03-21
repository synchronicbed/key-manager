﻿using System.Windows;
using Leosac.KeyManager.Library.Plugin;
using Leosac.KeyManager.Library.Plugin.UI.Domain;

namespace Leosac.KeyManager.Library.UI.Domain
{
    public class KeyStoreItem : ViewModelBase
    {
        private readonly KeyStoreUIFactory _factory;

        private KeyStorePropertiesControlViewModel? _dataContext;

        private object? _content;

        public KeyStoreItem(KeyStoreUIFactory factory, KeyStorePropertiesControlViewModel? dataContext = null)
        {
            _factory = factory;
            _dataContext = dataContext ?? factory.CreateKeyStorePropertiesControlViewModel();
        }

        public string Name { get => _factory.Name; }

        public KeyStoreUIFactory Factory { get => _factory; }

        public object? Content => _content ??= CreateContent();

        public KeyStorePropertiesControlViewModel? DataContext
        {
            get => _dataContext;
        }

        private object? CreateContent()
        {
            var content = _factory.CreateKeyStorePropertiesControl();
            if (_dataContext != null && content is FrameworkElement element)
            {
                element.DataContext = _dataContext;
            }
            return content;
        }
    }
}
