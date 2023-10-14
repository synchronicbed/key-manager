﻿using Leosac.KeyManager.Library.KeyStore;
using Leosac.KeyManager.Library.Plugin;
using Leosac.KeyManager.Library.Plugin.UI;
using Leosac.WpfApp;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Leosac.KeyManager.Library.UI.Domain
{
    public class KeyEntriesControlViewModel : ObservableValidator
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);
        public KeyEntriesControlViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            _snackbarMessageQueue = snackbarMessageQueue;
            _identifierLock = new object();
            Identifiers = new ObservableCollection<SelectableKeyEntryId>();
            BindingOperations.EnableCollectionSynchronization(Identifiers, _identifierLock);
            WizardFactories = new ObservableCollection<WizardFactory>(WizardFactory.RegisteredFactories);

            CreateKeyEntryCommand = new AsyncRelayCommand(
                async () =>
            {
                var model = new KeyEntryDialogViewModel() { KClass = _keClass };
                var dialog = new KeyEntryDialog()
                {
                    DataContext = model
                };
                await CreateKeyEntry(dialog);
            });

            EditKeyEntryCommand = new AsyncRelayCommand<SelectableKeyEntryId>(
                async identifier =>
            {
                try
                {
                    if (KeyStore != null && identifier?.KeyEntryId != null)
                    {
                        var model = new KeyEntryDialogViewModel()
                        {
                            KClass = _keClass,
                            KeyEntry = await KeyStore.Get(identifier.KeyEntryId, _keClass),
                            CanChangeFactory = false,
                            SubmitButtonText = Properties.Resources.Update
                        };
                        var factory = KeyEntryUIFactory.GetFactoryFromPropertyType(model.KeyEntry!.Properties?.GetType());
                        if (factory != null)
                        {
                            model.AutoCreate = false;
                            model.SelectedFactoryItem = model.KeyEntryFactories.Where(item => item.Factory == factory).FirstOrDefault();
                            model.SelectedFactoryItem!.DataContext!.Properties = model.KeyEntry.Properties;
                            var variant = model.KeyEntry.Variant;
                            if (variant != null)
                            {
                                model.RefreshVariants();
                                var emptyv = model.Variants.Where(v => v.Name == variant.Name).FirstOrDefault();
                                if (emptyv != null)
                                {
                                    model.Variants.Remove(emptyv);
                                }
                                model.Variants.Add(variant);
                                model.KeyEntry.Variant = variant;
                            }
                        }
                        var dialog = new KeyEntryDialog()
                        {
                            DataContext = model
                        };

                        await UpdateKeyEntry(dialog);
                    }
                }
                catch (KeyStoreException ex)
                {
                    SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex, "Key Store Error");
                }
                catch (Exception ex)
                {
                    log.Error("Unexpected error occured.", ex);
                }
            });

            MoveUpKeyEntryCommand = new AsyncRelayCommand<SelectableKeyEntryId>(
                async keyEntryId =>
            {
                if (keyEntryId != null)
                {
                    await MoveUpKeyEntry(keyEntryId);
                }
            });

            MoveDownKeyEntryCommand = new AsyncRelayCommand<SelectableKeyEntryId>(
                async keyEntryId =>
            {
                if (keyEntryId != null)
                {
                    await MoveDownKeyEntry(keyEntryId);
                }
            });

            DeleteKeyEntryCommand = new AsyncRelayCommand<SelectableKeyEntryId>(
                async keyEntryId =>
            {
                if (keyEntryId != null)
                {
                    await DeleteKeyEntry(keyEntryId);
                }
            });

            ImportCryptogramCommand = new AsyncRelayCommand<SelectableKeyEntryId>(
                async keyEntryId =>
            {
                var model = new ImportCryptogramDialogViewModel()
                {
                    CanChangeIdentifier = keyEntryId?.KeyEntryId == null || !keyEntryId.KeyEntryId.IsConfigured()
                };
                if (keyEntryId?.KeyEntryId != null)
                {
                    model.Cryptogram.Identifier = keyEntryId.KeyEntryId;
                }
                var dialog = new ImportCryptogramDialog()
                {
                    DataContext = model,
                };
                await ImportCryptogram(dialog);
            });

            WizardCommand = new AsyncRelayCommand<WizardFactory>(
                async factory =>
            {
                if (factory != null)
                {
                    await RunWizard(factory);
                }
            });

            ShowSelectionChangedCommand = new RelayCommand(
                () =>
            {
                if (!ShowSelection)
                {
                    ToggleAllSelection(false);
                }
            });

            ToggleSelectionCommand = new RelayCommand(
                () =>
                {
                    if (Identifiers.Count > 0)
                    {
                        ToggleAllSelection(!Identifiers[0].Selected);
                    }
                });

            _keClass = KeyEntryClass.Symmetric;
            _identifiersView = CollectionViewSource.GetDefaultView(Identifiers);
            _identifiersView.Filter = KeyEntryIdentifiersFilter;
        }

        protected ISnackbarMessageQueue _snackbarMessageQueue;
        private readonly object _identifierLock;
        private KeyStore.KeyStore? _keyStore;
        private KeyEntryClass _keClass;
        private bool _showSelection;
        private readonly ICollectionView _identifiersView;
        private string? _searchTerms;

        public ObservableCollection<SelectableKeyEntryId> Identifiers { get; }

        public ObservableCollection<WizardFactory> WizardFactories { get; }

        public KeyStore.KeyStore? KeyStore
        {
            get => _keyStore;
            set => SetProperty(ref _keyStore, value);
        }

        public KeyEntryClass KeyEntryClass
        {
            get => _keClass;
            set => SetProperty(ref _keClass, value);
        }

        public string? SearchTerms
        {
            get => _searchTerms;
            set
            {
                if (SetProperty(ref _searchTerms, value))
                {
                    RefreshKeyEntriesView();
                }
            }
        }

        public bool ShowSelection
        {
            get => _showSelection;
            set => SetProperty(ref _showSelection, value);
        }

        public AsyncRelayCommand CreateKeyEntryCommand { get; }

        private async Task CreateKeyEntry(KeyEntryDialog dialog)
        {
            var ret = await DialogHelper.ForceShow(dialog, "RootDialog");
            if (ret != null && dialog.DataContext is KeyEntryDialogViewModel model)
            {
                if (KeyStore != null && model.KeyEntry != null)
                {
                    try
                    {
                        await KeyStore.Create(model.KeyEntry);
                        Identifiers.Add(new SelectableKeyEntryId() {
                            Selected = false,
                            KeyEntryId = model.KeyEntry.Identifier
                        });
                    }
                    catch (KeyStoreException ex)
                    {
                        SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex, "Key Store Error");
                        await CreateKeyEntry(dialog);
                    }
                    catch (Exception ex)
                    {
                        log.Error("Creating the Key Entry failed unexpected.", ex);
                        SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex);
                        await CreateKeyEntry(dialog);
                    }
                }
            }
        }

        public AsyncRelayCommand<SelectableKeyEntryId> EditKeyEntryCommand { get; }

        private async Task UpdateKeyEntry(KeyEntryDialog dialog)
        {
            try
            {
                object? ret = await DialogHelper.ForceShow(dialog, "RootDialog");
                if (ret != null && dialog.DataContext is KeyEntryDialogViewModel model)
                {
                    if (KeyStore != null && model.KeyEntry != null)
                    {
                        await KeyStore.Update(model.KeyEntry);
                    }
                }
            }
            catch (KeyStoreException ex)
            {
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex, "Key Store Error");
                await UpdateKeyEntry(dialog);
            }
            catch (Exception ex)
            {
                log.Error("Updating the Key Entry failed unexpected.", ex);
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex);
                await UpdateKeyEntry(dialog);
            }
        }

        public AsyncRelayCommand<SelectableKeyEntryId> DeleteKeyEntryCommand { get; }

        private async Task DeleteKeyEntry(SelectableKeyEntryId identifier)
        {
            try
            {
                if (KeyStore != null && identifier.KeyEntryId != null)
                {
                    await KeyStore.Delete(identifier.KeyEntryId, _keClass);
                }
                Identifiers.Remove(identifier);
            }
            catch (KeyStoreException ex)
            {
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex, "Key Store Error");
            }
            catch (Exception ex)
            {
                log.Error("Deleting the Key Entry failed unexpected.", ex);
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex);
            }
        }

        public AsyncRelayCommand<SelectableKeyEntryId> MoveUpKeyEntryCommand { get; }

        private async Task MoveUpKeyEntry(SelectableKeyEntryId identifier)
        {
            try
            {
                if (KeyStore != null && identifier.KeyEntryId != null)
                {
                    await KeyStore.MoveUp(identifier.KeyEntryId, _keClass);
                }
                var oldIndex = Identifiers.IndexOf(identifier);
                if (oldIndex > 0)
                {
                    Identifiers.Move(oldIndex, oldIndex - 1);
                }
            }
            catch (KeyStoreException ex)
            {
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex, "Key Store Error");
            }
            catch (Exception ex)
            {
                log.Error("Moving Up the Key Entry failed unexpected.", ex);
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex);
            }
        }

        public AsyncRelayCommand<SelectableKeyEntryId> MoveDownKeyEntryCommand { get; }

        private async Task MoveDownKeyEntry(SelectableKeyEntryId identifier)
        {
            try
            {
                if (KeyStore != null && identifier.KeyEntryId != null)
                {
                    await KeyStore.MoveDown(identifier.KeyEntryId, _keClass);
                }
                var oldIndex = Identifiers.IndexOf(identifier);
                if (oldIndex != -1 && oldIndex < Identifiers.Count - 1)
                {
                    Identifiers.Move(oldIndex, oldIndex + 1);
                }
            }
            catch (KeyStoreException ex)
            {
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex, "Key Store Error");
            }
            catch (Exception ex)
            {
                log.Error("Moving Up the Key Entry failed unexpected.", ex);
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex);
            }
        }

        public AsyncRelayCommand<SelectableKeyEntryId> ImportCryptogramCommand { get; }

        private async Task ImportCryptogram(ImportCryptogramDialog dialog)
        {
            try
            {
                object? ret = await DialogHelper.ForceShow(dialog, "RootDialog");
                if (ret != null && dialog.DataContext is ImportCryptogramDialogViewModel model)
                {
                    if (KeyStore != null && !string.IsNullOrEmpty(model.Cryptogram.Value))
                    {
                        await KeyStore.Update(model.Cryptogram);
                    }
                }
            }
            catch (KeyStoreException ex)
            {
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex, "Key Store Error");
                await ImportCryptogram(dialog);
            }
            catch (Exception ex)
            {
                log.Error("Importing the Key Entry Cryptogram failed unexpected.", ex);
                SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex);
                await ImportCryptogram(dialog);
            }
        }

        public AsyncRelayCommand<WizardFactory> WizardCommand { get; }

        private async Task RunWizard(WizardFactory factory)
        {
            var w = factory.CreateWizardWindow();
            if (w.ShowDialog() == true)
            {
                try
                {
                    var entries = factory.GetKeyEntries(w);
                    if (KeyStore != null && entries != null && entries.Count > 0)
                    {
                        foreach (var entry in entries)
                        {
                            await KeyStore.Update(entry, true);
                        }
                        await RefreshKeyEntries();
                        SnackbarHelper.EnqueueMessage(_snackbarMessageQueue, "Wizard completed, key entries updated.");
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Updating the key store with resulting key entries from the wizard failed.", ex);
                    SnackbarHelper.EnqueueError(_snackbarMessageQueue, ex);
                }
            }
        }

        public RelayCommand ShowSelectionChangedCommand { get; }

        public RelayCommand ToggleSelectionCommand { get; }

        private void ToggleAllSelection(bool selected)
        {
            foreach (var identifier in Identifiers)
            {
                identifier.Selected = selected;
            }
        }

        public async Task RefreshKeyEntries()
        {
            lock (_identifierLock)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                Identifiers.Clear();
            }

            try
            {
                if (KeyStore != null)
                {
                    var ids = await KeyStore.GetAll(_keClass);
                    foreach (var id in ids)
                    {
                        lock (_identifierLock)
                        {
                            Identifiers.Add(new SelectableKeyEntryId()
                            {
                                Selected = false,
                                KeyEntryId = id
                            });
                        }
                    }
                }
            }
            finally
            {
                lock (_identifierLock)
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        public void RefreshKeyEntriesView()
        {
            _identifiersView.Refresh();
        }

        private bool KeyEntryIdentifiersFilter(object obj)
        {
            if (string.IsNullOrWhiteSpace(_searchTerms))
            {
                return true;
            }

            if (obj is SelectableKeyEntryId s && s.KeyEntryId != null)
            {
                obj = s.KeyEntryId;
            }

            if (obj is KeyEntryId item)
            {
                var terms = _searchTerms.ToLower();
                if (item.Id != null && item.Id.ToLower().Contains(terms))
                    return true;

                if (item.Label != null && item.Label.ToLower().Contains(terms))
                    return true;
            }

            return false;
        }
    }
}
