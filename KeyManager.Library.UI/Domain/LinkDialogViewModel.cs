﻿using Leosac.KeyManager.Library.KeyStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leosac.KeyManager.Library.UI.Domain
{
    public abstract class LinkDialogViewModel : ViewModelBase
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        public LinkDialogViewModel()
        {
            RunLinkCommand = new KeyManagerCommand(
                parameter =>
                {
                    RunLink();
                });
        }

        private Link? _link;

        public Link? Link

        {
            get => _link;
            set => SetProperty(ref _link, value);
        }

        private string? _linkResult;

        public string? LinkResult
        {
            get => _linkResult;
            set => SetProperty(ref _linkResult, value);
        }

        private string? _linkError;

        public string? LinkError
        {
            get => _linkError;
            set => SetProperty(ref _linkError, value);
        }

        private string? _divInputResult;

        public string? DivInputResult
        {
            get => _divInputResult;
            set => SetProperty(ref _divInputResult, value);
        }

        public KeyManagerCommand RunLinkCommand { get; }

        public abstract void RunLinkImpl(KeyStore.KeyStore ks);

        public void RunLink()
        {
            log.Info(String.Format("Running the link manually..."));
            LinkError = null;
            if (Link != null && !string.IsNullOrEmpty(Link.KeyStoreFavorite) && !string.IsNullOrEmpty(Link.KeyIdentifier))
            {
                var fav = Favorites.GetSingletonInstance().KeyStores.Where(f => f.Name.ToLower() == Link.KeyStoreFavorite.ToLower()).FirstOrDefault();
                if (fav != null)
                {
                    var ks = fav.CreateKeyStore();
                    if (ks != null)
                    {
                        try
                        {
                            ks.Open();
                            RunLinkImpl(ks);
                            ks.Close();

                            log.Info(String.Format("Link execution completed."));
                        }
                        catch (KeyStoreException ex)
                        {
                            LinkError = ex.Message;
                        }
                        catch (Exception ex)
                        {
                            log.Error(String.Format("Unexpected error when resolving the link: {0}", ex.Message));
                            LinkError = String.Format("Unexpected error: {0}", ex.Message);
                        }
                    }
                    else
                    {
                        log.Error(String.Format("Cannot create the key store from Favorite `{0}`.", Link.KeyStoreFavorite));
                        LinkError = "Cannot create the key store from Favorite.";
                    }
                }
                else
                {
                    log.Error(String.Format("Cannot found the linked key store `{0}`.", Link.KeyStoreFavorite));
                    LinkError = "Cannot found the linked key store.";
                }
            }
            else
            {
                log.Error("The Link is not correctly setup.");
                LinkError = "The Link is not correctly setup.";
            }
        }
    }
}