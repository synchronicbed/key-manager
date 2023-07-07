﻿using Leosac.KeyManager.Library.Plugin.UI;
using Leosac.KeyManager.Library.KeyStore.NXP_SAM.UI.Wizard.Domain;
using System.Windows;

namespace Leosac.KeyManager.Library.KeyStore.NXP_SAM.UI.Wizard
{
    public class SAMAccessControlWizardFactory : WizardFactory
    {
        public override string Name => Properties.Resources.AccessControlWizard;

        public override Type? GetPropertiesType()
        {
            return null;
        }

        public override Window CreateWizardWindow()
        {
            return new SAMAccessControlWizardWindow();
        }

        public override IList<Leosac.KeyManager.Library.KeyStore.KeyEntry> GetKeyEntries(Window window)
        {
            var entries = new List<Leosac.KeyManager.Library.KeyStore.KeyEntry>();
            if (window is SAMAccessControlWizardWindow w)
            {
                var model = w.DataContext as SAMAccessControlWizardWindowViewModel;
                if (model!.ChangeSAMMasterKey)
                {
                    var masterke = CreateKeyEntry("0", "SAM Master Key");
                    masterke.SAMProperties!.LockUnlock = true;
                    entries.Add(masterke);
                }
                if (model.ChangeSAMUnlockKey)
                {
                    var unlockke = CreateKeyEntry("1", "SAM Unlock Key");
                    unlockke.SAMProperties!.LockUnlock = true;
                    unlockke.SAMProperties.AuthenticateHost = false;
                    entries.Add(unlockke);
                }
                var piccke = CreateKeyEntry("2", "DESFire Read Key", false);
                piccke.SAMProperties!.SAMKeyEntryType = SAMKeyEntryType.PICC;
                piccke.SAMProperties.KeepIV = true;
                piccke.SAMProperties.EnableDumpSessionKey = true;
                piccke.SAMProperties.DisableChangeKeyPICC = true;
                piccke.SAMProperties.AuthenticateHost = false;
                piccke.SAMProperties.DESFireAID = model.PICCAID;
                piccke.SAMProperties.DESFireKeyNum = model.PICCKeyNo;
                piccke.Variant!.KeyContainers[0] = model.PICCKey;
                entries.Add(piccke);
            }
            return entries;
        }

        private SAMSymmetricKeyEntry CreateKeyEntry(string id, string label, bool generateKeys = true)
        {
            var ke = new SAMSymmetricKeyEntry();
            ke.Identifier.Id = id;
            ke.Identifier.Label = label;
            ke.Variant = ke.GetAllVariants().Where(v => v.Name == "AES128").FirstOrDefault();
            if (generateKeys)
            {
                for (byte i = 0; i < ke.Variant!.KeyContainers.Count; ++i)
                {
                    var keyVersion = ke.Variant.KeyContainers[i] as KeyVersion;
                    if (keyVersion != null)
                    {
                        keyVersion.Version = i;
                        keyVersion.Key.Materials[0].Value = KeyGeneration.Random((int)keyVersion.Key.KeySize);
                    }
                }
            }
            return ke;
        }
    }
}