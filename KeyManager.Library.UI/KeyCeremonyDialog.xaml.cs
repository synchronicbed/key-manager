﻿using Leosac.KeyManager.Library.UI.Domain;
using System.Windows;

namespace Leosac.KeyManager.Library.UI
{
    /// <summary>
    /// Interaction logic for KeyCeremonyDialog.xaml
    /// </summary>
    public partial class KeyCeremonyDialog : Window
    {
        public KeyCeremonyDialog()
        {
            InitializeComponent();
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            while (transition.Items.Count > 2)
            {
                transition.Items.RemoveAt(1);
            }

            if (DataContext is KeyCeremonyDialogViewModel model)
            {
                for (int i = 0; i < model.Fragments.Count; ++i)
                {
                    transition.Items.Insert(i + 1, new KeyCeremonyFragmentControl
                    {
                        DataContext = new KeyCeremonyFragmentControlViewModel
                        {
                            Fragment = i + 1,
                            IsReunification = model.IsReunification,
                            FragmentValue = model.Fragments[i]
                        }
                    });
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is KeyCeremonyDialogViewModel model)
            {
                if (model.IsReunification)
                {
                    for (int i = 0; i < model.Fragments.Count && (i + 1) < transition.Items.Count; ++i)
                    {
                        if (transition.Items[i + 1] is KeyCeremonyFragmentControl fragmentControl)
                        {
                            if (fragmentControl.DataContext is KeyCeremonyFragmentControlViewModel fragmentControlModel)
                            {
                                model.Fragments[i] = fragmentControlModel.FragmentValue;
                            }
                        }
                    }
                }
                this.DialogResult = true;
            }
        }
    }
}
