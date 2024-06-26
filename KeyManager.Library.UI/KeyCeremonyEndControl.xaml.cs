﻿using CommunityToolkit.Mvvm.Input;
using Leosac.KeyManager.Library.UI.Domain;
using Leosac.WpfApp.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Leosac.KeyManager.Library.UI
{
    /// <summary>
    /// Interaction logic for KeyCeremonyEndControl.xaml
    /// </summary>
    public partial class KeyCeremonyEndControl : UserControl
    {
        public KeyCeremonyEndControl()
        {
            InitializeComponent();
        }

        public RelayCommand? CloseCommand { get; set; }
    }
}
