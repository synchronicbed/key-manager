﻿/*
** File Name: SAM_SEKeyStoreUIFactory.cs
** Author: s_eva
** Creation date: January 2024
** Description: This file is a factory one. It ensures that we have all the correct object to work with.
** Licence: LGPLv3
** Copyright (c) 2023-Present Synchronic
*/

using Leosac.KeyManager.Library.KeyStore.SAM_SE.UI.Domain;
using Leosac.KeyManager.Library.Plugin;
using Leosac.KeyManager.Library.Plugin.UI.Domain;
using System.Windows.Controls;

namespace Leosac.KeyManager.Library.KeyStore.SAM_SE.UI
{
    public class SAM_SEKeyStoreUIFactory : KeyStoreUIFactory
    {
        public SAM_SEKeyStoreUIFactory()
        {
            targetFactory = new SAM_SEKeyStoreFactory();
        }

        public override string Name => "NXP SAM-SE (Synchronic)";

        public override Type GetPropertiesType()
        {
            return typeof(SAM_SEKeyStoreProperties);
        }

        public override UserControl CreateKeyStorePropertiesControl()
        {
            return new SAM_SEKeyStorePropertiesControl();
        }

        public override KeyStorePropertiesControlViewModel? CreateKeyStorePropertiesControlViewModel()
        {
            return new SAM_SEKeyStorePropertiesControlViewModel();
        }

        public override IDictionary<string, UserControl> CreateKeyStoreAdditionalControls()
        {
            var controls = new Dictionary<string, UserControl>
            {
                { Properties.Resources.Tools, new SAM_SEKeyStoreToolsControl() }
            };
            return controls;
        }
    }
}
