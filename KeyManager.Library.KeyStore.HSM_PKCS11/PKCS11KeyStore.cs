﻿using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Cms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Leosac.KeyManager.Library.KeyStore.HSM_PKCS11
{
    public class PKCS11KeyStore : KeyStore
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        public PKCS11KeyStore()
        {

        }

        private IPkcs11Library? _library;
        private ISlot? _slot;
        private ISession? _session;

        public override string Name => "PKCS#11";

        public override bool CanCreateKeyEntries => true;

        public override bool CanDeleteKeyEntries => true;

        public override IEnumerable<KeyEntryClass> SupportedClasses
        {
            get => new KeyEntryClass[] { KeyEntryClass.Symmetric, KeyEntryClass.Asymmetric };
        }

        public override bool CheckKeyEntryExists(KeyEntryId identifier, KeyEntryClass keClass)
        {
            IObjectHandle? handle;
            return CheckKeyEntryExists(identifier, keClass, out handle);
        }

        public bool CheckKeyEntryExists(KeyEntryId identifier, out IObjectHandle? handle)
        {
            return CheckKeyEntryExists(identifier, null, out handle);
        }

        public bool CheckKeyEntryExists(KeyEntryId identifier, KeyEntryClass? keClass, out IObjectHandle? handle)
        {
            if (_session == null)
                throw new KeyStoreException("No valid session.");

            if (identifier.Handle != null && identifier.Handle is IObjectHandle h)
            {
                handle = h;
                return true;
            }

            var attributes = new List<IObjectAttribute>();
            if (!string.IsNullOrEmpty(identifier.Id))
                attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, Convert.FromHexString(identifier.Id)));
            if (!string.IsNullOrEmpty(identifier.Label))
                attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, UTF8Encoding.UTF8.GetBytes(identifier.Label)));

            var objects = new List<IObjectHandle>();
            if (keClass != null)
            {
                if (keClass == KeyEntryClass.Symmetric)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_SECRET_KEY));
                }
                else if (keClass == KeyEntryClass.Asymmetric)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY));
                    objects = _session.FindAllObjects(attributes);
                    attributes[attributes.Count - 1] = _session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PUBLIC_KEY);
                }
                else if (keClass == KeyEntryClass.PrivateKey)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY));
                }
                else if (keClass == KeyEntryClass.PublicKey)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PUBLIC_KEY));
                }
            }

            if (attributes.Count == 0)
                throw new KeyStoreException("No key identifier.");

            objects.Union(_session.FindAllObjects(attributes));
            if (objects.Count > 0)
            {
                handle = objects[0];
                return true;
            }

            handle = null;
            return false;
        }

        public override void Close()
        {
            log.Info("Closing the key store...");
            if (_session != null)
            {
                _session.CloseSession();
            }
            if (_slot != null)
            {
                _slot.CloseAllSessions();
                _slot = null;
            }
            if (_library != null)
            {
                _library.Dispose();
                _library = null;
            }
            log.Info("Key Store closed.");
        }

        public override void 
            Create(IChangeKeyEntry change)
        {
            log.Info(String.Format("Creating key entry `{0}`...", change.Identifier));

            if (change is KeyEntry entry && entry.Variant != null && entry.Variant.KeyContainers.Count > 0)
            {
                foreach (var material in entry.Variant.KeyContainers[0].Key.Materials)
                {
                    var rawkey = material.GetFormattedValue<byte[]>(KeyValueFormat.Binary);
                    if (rawkey != null)
                    {
                        var attributes = GetKeyEntryAttributes(entry, true);
                        attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_VALUE, rawkey));
                        if (entry.KClass == KeyEntryClass.PrivateKey || (entry.KClass == KeyEntryClass.Asymmetric && material.Name == KeyMaterial.PRIVATE_KEY))
                            attributes.Add(_session!.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY));
                        else if (entry.KClass == KeyEntryClass.PublicKey || (entry.KClass == KeyEntryClass.Asymmetric && material.Name == KeyMaterial.PUBLIC_KEY))
                            attributes.Add(_session!.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PUBLIC_KEY));
                        else
                            attributes.Add(_session!.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_SECRET_KEY));
                        _session!.CreateObject(attributes);
                    }
                }
            }
            else if (change is KeyEntryCryptogram cryptogram)
            {
                if (cryptogram.WrappingKeyId == null)
                {
                    log.Error("Wrapping Key Entry Identifier parameter is expected.");
                    throw new KeyStoreException("Wrapping Key Entry Identifier parameter is expected.");
                }

                IObjectHandle? wrapHandle;
                if (!CheckKeyEntryExists(cryptogram.WrappingKeyId, out wrapHandle))
                {
                    log.Error(String.Format("The key entry `{0}` doesn't exist.", cryptogram.WrappingKeyId));
                    throw new KeyStoreException("The key entry doesn't exist.");
                }
                var attributes = GetKeyEntryAttributes(null, true);
                var mechanism = CreateMostExpectedWrappingMechanism(wrapHandle!);
                _session!.UnwrapKey(mechanism, wrapHandle, Convert.FromHexString(cryptogram.Value), attributes);
            }

            log.Info(String.Format("Key entry `{0}` created.", change.Identifier));
        }

        private List<IObjectAttribute> GetKeyEntryAttributes(KeyEntry? entry, bool create = false)
        {
            if (entry != null && entry.Variant?.KeyContainers.Count > 1)
            {
                throw new KeyStoreException("This key store do not support key entries with more than one key container.");
            }

            var attributes = new List<IObjectAttribute>();
            if (entry != null)
            {
                if (entry.Identifier.Id != null && create)
                    attributes.Add(_session!.Factories.ObjectAttributeFactory.Create(CKA.CKA_ID, Convert.FromHexString(entry.Identifier.Id)));
                if (entry.Identifier.Label != null)
                    attributes.Add(_session!.Factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, UTF8Encoding.UTF8.GetBytes(entry.Identifier.Label)));
            }
            if (entry is PKCS11KeyEntry pkcsEntry)
            {
                if (create)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_KEY_TYPE, pkcsEntry.GetCKK()));
                    if (pkcsEntry.PKCS11Properties.Extractable != null)
                        attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_EXTRACTABLE, pkcsEntry.PKCS11Properties.Extractable.Value));
                }
                if (pkcsEntry.PKCS11Properties!.Encrypt != null)
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ENCRYPT, pkcsEntry.PKCS11Properties!.Encrypt.Value));
                if (pkcsEntry.PKCS11Properties.Decrypt != null)
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_DECRYPT, pkcsEntry.PKCS11Properties.Decrypt.Value));
                if (pkcsEntry.PKCS11Properties.Derive != null)
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_DERIVE, pkcsEntry.PKCS11Properties.Derive.Value));
            }
            else
            {
                if (create && entry != null)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_KEY_TYPE, PKCS11KeyEntry.GetCKK(entry)));
                }
                attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_ENCRYPT, true));
                attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_DECRYPT, true));
                attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_DERIVE, true));
                attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_EXTRACTABLE, true));
            }

            if (create)
            {
                attributes.Add(_session!.Factories.ObjectAttributeFactory.Create(CKA.CKA_TOKEN, true));
            }

            return attributes;
        }

        public override void Delete(KeyEntryId identifier, KeyEntryClass keClass, bool ignoreIfMissing = false)
        {
            log.Info(String.Format("Deleting key entry `{0}`...", identifier));
            IObjectHandle? handle;
            var exists = CheckKeyEntryExists(identifier, keClass, out handle);
            if (!exists && !ignoreIfMissing)
            {
                log.Error(String.Format("The key entry `{0}` doesn't exist.", identifier));
                throw new KeyStoreException("The key entry doesn't exist.");
            }

            if (exists)
            {
                _session!.DestroyObject(handle);
                log.Info(String.Format("Key entry `{0}` deleted.", identifier));
            }
        }

        public override KeyEntry? Get(KeyEntryId identifier, KeyEntryClass keClass)
        {
            log.Info(String.Format("Getting key entry `{0}`...", identifier));
            IObjectHandle? handle;
            if (!CheckKeyEntryExists(identifier, keClass, out handle))
            {
                log.Error(String.Format("The key entry `{0}` doesn't exist.", identifier));
                throw new KeyStoreException("The key entry doesn't exist.");
            }

            var attributes = _session!.GetAttributeValue(handle, new List<CKA>
            {
                CKA.CKA_CLASS
            });
            var cko = (CKO)attributes[0].GetValueAsUlong();

            attributes = _session!.GetAttributeValue(handle, new List<CKA>
            {
                CKA.CKA_KEY_TYPE,
                CKA.CKA_ID,
                CKA.CKA_LABEL
            });

            PKCS11KeyEntry keyEntry;
            if (cko == CKO.CKO_SECRET_KEY)
                keyEntry = new SymmetricPKCS11KeyEntry();
            else if (cko == CKO.CKO_PRIVATE_KEY)
                keyEntry = new AsymmetricPKCS11KeyEntry(KeyEntryClass.PrivateKey);
            else if (cko == CKO.CKO_PUBLIC_KEY)
                keyEntry = new AsymmetricPKCS11KeyEntry(KeyEntryClass.PublicKey);
            else
                throw new KeyStoreException("Unsupported CKO.");

            keyEntry.GetAttributes(_session, handle);
            keyEntry.Identifier = identifier;
            foreach (var attribute in attributes)
            {
                if (attribute.Type == (ulong)CKA.CKA_KEY_TYPE)
                    keyEntry.Variant = keyEntry.CreateVariantFromCKK((CKK)attribute.GetValueAsUlong());
                else if (attribute.Type == (ulong)CKA.CKA_ID)
                    keyEntry.Identifier.Id = Convert.ToHexString(attribute.GetValueAsByteArray());
                else if (attribute.Type == (ulong)CKA.CKA_LABEL)
                    keyEntry.Identifier.Label = attribute.GetValueAsString();
                else
                    throw new KeyStoreException("Unexpected attribute.");
            }

            var materials = keyEntry.Variant!.KeyContainers[0].Key.Materials;
            if (materials.Count > 0)
            {
                if (cko == CKO.CKO_PUBLIC_KEY)
                {
                    materials[0].Name = KeyMaterial.PUBLIC_KEY;
                }
                else if (cko == CKO.CKO_PRIVATE_KEY)
                {
                    materials[0].Name = KeyMaterial.PRIVATE_KEY;
                }
            }

            log.Info(String.Format("Key entry `{0}` retrieved.", identifier));
            return keyEntry;
        }

        public override IList<KeyEntryId> GetAll(KeyEntryClass? keClass = null)
        {
            log.Info(String.Format("Getting all key entries (class: `{0}`)...", keClass));
            var entries = new List<KeyEntryId>();

            if (_session == null)
                throw new KeyStoreException("No valid session.");

            var attributes = new List<IObjectAttribute>
            {
                _session.Factories.ObjectAttributeFactory.Create(CKA.CKA_TOKEN, true)
            };

            var objects = new List<IObjectHandle>();
            if (keClass != null)
            {
                if (keClass == KeyEntryClass.Symmetric)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_SECRET_KEY));
                }
                else if (keClass == KeyEntryClass.Asymmetric)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY));
                    objects = _session.FindAllObjects(attributes);
                    attributes[1] = _session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PUBLIC_KEY);
                }
                else if (keClass == KeyEntryClass.PrivateKey)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY));
                }
                else if (keClass == KeyEntryClass.PublicKey)
                {
                    attributes.Add(_session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PUBLIC_KEY));
                }
            }

            objects.Union(_session.FindAllObjects(attributes));
            foreach (var obj in objects)
            {
                if (obj.ObjectId != CK.CK_INVALID_HANDLE)
                {
                    var objAttributes = _session.GetAttributeValue(obj, new List<CKA>
                    {
                        CKA.CKA_ID,
                        CKA.CKA_LABEL
                    });

                    var entry = new KeyEntryId();
                    entry.Handle = obj;
                    if (!objAttributes[0].CannotBeRead)
                        entry.Id = Convert.ToHexString(objAttributes[0].GetValueAsByteArray());
                    if (!objAttributes[1].CannotBeRead)
                        entry.Label = objAttributes[1].GetValueAsString();

                    entries.Add(entry);
                }
                else
                {
                    log.Warn("Object with invalid handle returned. Skipped.");
                }
            }

            log.Info(String.Format("{0} key entries returned.", entries.Count));
            return entries;
        }

        public PKCS11KeyStoreProperties GetPKCS11Properties()
        {
            var p = Properties as PKCS11KeyStoreProperties;
            if (p == null)
                throw new KeyStoreException("Missing PKCS#11 key store properties.");
            return p;
        }

        public override void Open()
        {
            log.Info("Opening the key store...");
            var factories = new Pkcs11InteropFactories();
            _library = factories.Pkcs11LibraryFactory.LoadPkcs11Library(factories, GetPKCS11Properties().LibraryPath, AppType.MultiThreaded);

            var slots = _library.GetSlotList(SlotsType.WithTokenPresent);
            if (slots.Count == 0)
            {
                log.Error("No slots with token available.");
                throw new KeyStoreException("No slots with token available.");
            }

            var prop = GetPKCS11Properties();
            if (string.IsNullOrEmpty(prop.SlotFilter))
            {
                _slot = slots[0];
            }
            else
            {
                foreach (var slot in slots)
                {
                    if (prop.SlotFilterType == SlotFilterType.SlotId && prop.SlotFilter == slot.SlotId.ToString())
                    {
                        _slot = slot;
                        break;
                    }

                    var token = slot.GetTokenInfo();
                    if (token != null)
                    {
                        if (prop.SlotFilterType == SlotFilterType.TokenLabel && token.Label == prop.SlotFilter)
                        {
                            _slot = slot;
                            break;
                        }

                        if (prop.SlotFilterType == SlotFilterType.TokenSerial && token.SerialNumber == prop.SlotFilter)
                        {
                            _slot = slot;
                            break;
                        }
                    }
                }

                if (_slot == null)
                {
                    log.Error(String.Format("Cannot found expected slot (Filter Type: `{0}`, Filter: `{1}`).", prop.SlotFilterType, prop.SlotFilter));
                    throw new KeyStoreException("Cannot found expected slot.");
                }
                log.Info("Expected slot found.");
            }

            _session = _slot.OpenSession(SessionType.ReadWrite);
            _session.Login(GetPKCS11Properties().User, GetPKCS11Properties().GetUserPINBytes());

            log.Info("Key Store opened.");
        }

        public override string? ResolveKeyEntryLink(KeyEntryId keyIdentifier, KeyEntryClass keClass, string? divInput = null, KeyEntryId? wrappingKeyId = null, string? wrappingContainerSelector = null)
        {
            log.Info(String.Format("Resolving key entry link with Key Entry Identifier `{0}` and Wrapping Key Entry Identifier...", keyIdentifier, wrappingKeyId));
            if (wrappingKeyId == null)
            {
                log.Error("Wrapping Key Entry Identifier parameter is expected.");
                throw new KeyStoreException("Wrapping Key Entry Identifier parameter is expected.");
            }
            if (!string.IsNullOrEmpty(divInput))
            {
                log.Error("Div Input parameter is not supported.");
                throw new KeyStoreException("Div Input parameter is not supported.");
            }

            IObjectHandle? handle;
            if (!CheckKeyEntryExists(keyIdentifier, keClass, out handle))
            {
                log.Error(String.Format("The key entry `{0}` doesn't exist.", keyIdentifier));
                throw new KeyStoreException("The key entry doesn't exist.");
            }
            IObjectHandle? wrapHandle;
            if (!CheckKeyEntryExists(wrappingKeyId, out wrapHandle))
            {
                log.Error(String.Format("The key entry `{0}` doesn't exist.", wrappingKeyId));
                throw new KeyStoreException("The key entry doesn't exist.");
            }

            IMechanism mechanism = CreateMostExpectedWrappingMechanism(wrapHandle!);
            log.Info(String.Format("Using mechanism {0}...", (CKM)mechanism.Type));
            var data = _session!.WrapKey(mechanism, wrapHandle, handle);
            log.Info("Key entry link completed.");
            return Convert.ToHexString(data);
        }

        protected IMechanism CreateMostExpectedWrappingMechanism(IObjectHandle handle)
        {
            CKM ckm;
            var attributes = _session!.GetAttributeValue(handle, new List<CKA>
            {
                CKA.CKA_KEY_TYPE
            });
            var ckk = (CKK)attributes[0].GetValueAsUlong();

            switch (ckk)
            {
                case CKK.CKK_AES:
                    ckm = CKM.CKM_AES_CBC;
                    break;
                case CKK.CKK_DES:
                    ckm = CKM.CKM_DES_CBC;
                    break;
                case CKK.CKK_DES2:
                case CKK.CKK_DES3:
                    ckm = CKM.CKM_DES3_CBC;
                    break;
                case CKK.CKK_DSA:
                    ckm = CKM.CKM_DSA;
                    break;
                case CKK.CKK_ECDSA:
                    ckm = CKM.CKM_ECDSA;
                    break;
                case CKK.CKK_RSA:
                    ckm = CKM.CKM_RSA_PKCS;
                    break;
                default:
                    throw new KeyStoreException("Unsupported key type");
            }

            return _session.Factories.MechanismFactory.Create(ckm);
        }

        public override string? ResolveKeyLink(KeyEntryId keyIdentifier, KeyEntryClass keClass, string? containerSelector = null, string? divInput = null)
        {
            log.Info(String.Format("Resolving key link with Key Entry Identifier `{0}`...", keyIdentifier));
            if (!string.IsNullOrEmpty(divInput))
            {
                log.Error("Div Input parameter is not supported.");
                throw new KeyStoreException("Div Input parameter is not supported.");
            }

            IObjectHandle? handle;
            if (!CheckKeyEntryExists(keyIdentifier, keClass, out handle))
            {
                log.Error(String.Format("The key entry `{0}` doesn't exist.", keyIdentifier));
                throw new KeyStoreException("The key entry doesn't exist.");
            }

            var attributes = _session!.GetAttributeValue(handle, new List<CKA>
            {
                CKA.CKA_VALUE
            });
            log.Info("Key link completed.");
            return Convert.ToHexString(attributes[0].GetValueAsByteArray());
        }

        public override void Store(IList<IChangeKeyEntry> changes)
        {
            log.Info(String.Format("Storing `{0}` key entries...", changes.Count));

            foreach (var change in changes)
            {
                if (CheckKeyEntryExists(change.Identifier, change.KClass))
                {
                    Update(change);
                }
                else
                {
                    Create(change);
                }
            }

            log.Info("Key Entries storing completed.");
        }

        public override void Update(IChangeKeyEntry change, bool ignoreIfMissing = false)
        {
            log.Info(String.Format("Updating key entry `{0}`...", change.Identifier));

            IObjectHandle? handle;
            if (!CheckKeyEntryExists(change.Identifier, change.KClass, out handle))
            {
                log.Error(String.Format("The key entry `{0}` doesn't exist.", change.Identifier));
                throw new KeyStoreException("The key entry doesn't exist.");
            }

            if (change is KeyEntry entry)
            {
                var attributes = GetKeyEntryAttributes(entry);
                if (entry.Variant?.KeyContainers.Count == 1)
                {
                    var rawkey = entry.Variant.KeyContainers[0].Key.GetAggregatedValue<byte[]>(KeyValueFormat.Binary);
                    if (rawkey != null)
                    {
                        // We should already have only one key material during an update
                        attributes.Add(_session!.Factories.ObjectAttributeFactory.Create(CKA.CKA_VALUE, rawkey));
                    }
                }
                _session!.SetAttributeValue(handle, attributes);
            }
            else
                throw new NotImplementedException();

            log.Info(String.Format("Key entry `{0}` updated.", change.Identifier));
        }
    }
}