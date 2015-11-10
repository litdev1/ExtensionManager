using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ExtensionManagerLibrary
{
    class EMButton : Button
    {
        public enum eState { NONE, INSTALL, UPDATE, INSTALLED, DISABLED }
        private eState state = eState.NONE;
        public Rectangle bar = new Rectangle();
        private double zipSize = 0;

        public EMButton(Extension extension)
        {
            TextBlock textBlock = new TextBlock();
            this.Content = textBlock;
            textBlock.Text = extension.Name;
            textBlock.Foreground = new SolidColorBrush(Colors.Black);
            textBlock.TextDecorations = null;
            textBlock.FontFamily = new FontFamily("Consolas");
            textBlock.FontSize = 18;
            textBlock.FontWeight = FontWeights.Normal;

            Size size = new Size(double.MaxValue, double.MaxValue);
            textBlock.Measure(size);
            this.Width = 30 + textBlock.DesiredSize.Width;
            this.Height = 30;
            this.Tag = extension;
            this.BorderThickness = new Thickness(0);
            this.Background = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));

            bar.StrokeThickness = 0;
            bar.Width = 5;
            bar.Height = 30;

            ToolTip tooltip = new ToolTip();
            this.ToolTip = tooltip;
            tooltip.Content = "";
            tooltip.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 255));
            tooltip.Background = new SolidColorBrush(Colors.Transparent);
            tooltip.BorderThickness = new Thickness(0);
            tooltip.FontSize = 14;
            tooltip.FontFamily = new FontFamily("Consolas");
            ToolTipService.SetInitialShowDelay(this, 0);
            ToolTipService.SetHorizontalOffset(this, 4);
            ToolTipService.SetVerticalOffset(this, 3);
            ToolTipService.SetPlacement(this, PlacementMode.Right);
            ToolTipService.SetHasDropShadow(this, false);

            ContextMenu menu = new ContextMenu();
            this.ContextMenu = menu;
            Image img;
            MenuItem item;

            item = new MenuItem();
            item.IsChecked = false;
            item.Header = "";
            img = new Image();
            img.Source = EMWindow.GetBitmapImage(Properties.Resources.install);
            item.Icon = img;
            item.Tag = this;
            item.Name = "Install";
            item.IsEnabled = false;
            menu.Items.Add(item);

            item = new MenuItem();
            item.IsChecked = false;
            item.Header = "Uninstall";
            img = new Image();
            img.Source = EMWindow.GetBitmapImage(Properties.Resources.uninstall);
            item.Icon = img;
            item.Tag = this;
            item.Name = "Uninstall";
            menu.Items.Add(item);

            item = new MenuItem();
            item.IsChecked = false;
            item.Header = "Toggle enable/disable";
            img = new Image();
            img.Source = EMWindow.GetBitmapImage(Properties.Resources.disable);
            item.Icon = img;
            item.Tag = this;
            item.Name = "Disable";
            menu.Items.Add(item);

            item = new MenuItem();
            item.IsChecked = false;
            item.Header = "Small Basic version : "+ extension.SBVersion.ToString();
            img = new Image();
            img.Source = Imaging.CreateBitmapSourceFromHIcon(Properties.Resources.AppIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            item.Icon = img;
            item.Tag = this;
            item.Name = "SBVersion";
            item.IsEnabled = false;
            menu.Items.Add(item);

            item = new MenuItem();
            item.IsChecked = false;
            item.Header = "Extension version : " + extension.ExtVersion.ToString();
            img = new Image();
            img.Source = EMWindow.GetBitmapImage(Properties.Resources.extension);
            item.Icon = img;
            item.Tag = this;
            item.Name = "ExtVersion";
            item.IsEnabled = false;
            menu.Items.Add(item);

            if (null != extension.InstalledVersion)
            {
                item = new MenuItem();
                item.IsChecked = false;
                item.Header = "Installed version : " + extension.InstalledVersion.ToString();
                img = new Image();
                img.Source = EMWindow.GetBitmapImage(Properties.Resources.extension);
                item.Icon = img;
                item.Tag = this;
                item.Name = "InstalledVersion";
                item.IsEnabled = false;
                menu.Items.Add(item);
            }

            if (extension.Source == eSource.WEB && null != extension.smallBasicExtension)
            {
                WebRequest webRequest = HttpWebRequest.Create(extension.smallBasicExtension.ZipLocation);
                webRequest.Method = "HEAD";
                WebResponse webResponse = webRequest.GetResponse();
                zipSize = webResponse.ContentLength;
                webResponse.Close();

                if (extension.smallBasicExtension.Description != "")
                {
                    item = new MenuItem();
                    item.IsChecked = false;
                    item.Header = "Description : " + extension.smallBasicExtension.Description;
                    img = new Image();
                    img.Source = EMWindow.GetBitmapImage(Properties.Resources.info);
                    item.Icon = img;
                    item.Tag = this;
                    item.Name = "Description";
                    item.IsEnabled = false;
                    menu.Items.Add(item);
                }

                if (null != extension.smallBasicExtension.WebSite)
                {
                    item = new MenuItem();
                    item.IsChecked = false;
                    item.Header = "Visit website";
                    img = new Image();
                    img.Source = EMWindow.GetBitmapImage(Properties.Resources.webopen);
                    item.Icon = img;
                    item.Tag = this;
                    item.Name = "WebSite";
                    menu.Items.Add(item);
                }

                if (null != extension.smallBasicExtension.API)
                {
                    item = new MenuItem();
                    item.IsChecked = false;
                    item.Header = "View API documentation";
                    img = new Image();
                    img.Source = EMWindow.GetBitmapImage(Properties.Resources.API);
                    item.Icon = img;
                    item.Tag = this;
                    item.Name = "API";
                    menu.Items.Add(item);
                }

                if (null != extension.smallBasicExtension.ChangeLog)
                {
                    item = new MenuItem();
                    item.IsChecked = false;
                    item.Header = "View change log";
                    img = new Image();
                    img.Source = EMWindow.GetBitmapImage(Properties.Resources.changeLog);
                    item.Icon = img;
                    item.Tag = this;
                    item.Name = "ChangeLog";
                    menu.Items.Add(item);
                }

                if (extension.smallBasicExtension.ZipLocation != "")
                {
                    item = new MenuItem();
                    item.IsChecked = false;
                    item.Header = "Save download folder to Documents";
                    img = new Image();
                    img.Source = EMWindow.GetBitmapImage(Properties.Resources.download);
                    item.Icon = img;
                    item.Tag = this;
                    item.Name = "SaveZip";
                    menu.Items.Add(item);
                }
            }
        }

        public void SetState(eState state)
        {
            this.state = state;
            switch (state)
            {
                case eState.INSTALL:
                    {
                        TextBlock textBlock = (TextBlock)(this.Content);
                        //this.Background = new SolidColorBrush(Colors.SlateBlue);
                        //textBlock.Foreground = new SolidColorBrush(Colors.MediumBlue);
                        //textBlock.TextDecorations = TextDecorations.Underline;
                        ((MenuItem)this.ContextMenu.Items[0]).Header = "Install";
                        ((MenuItem)this.ContextMenu.Items[0]).IsEnabled = true;
                        ((MenuItem)this.ContextMenu.Items[1]).IsEnabled = false;
                        ((MenuItem)this.ContextMenu.Items[2]).IsEnabled = false;
                        bar.Fill = new SolidColorBrush(Colors.MediumSlateBlue);
                        ((ToolTip)this.ToolTip).Content = "Click to install";
                    }
                    break;
                case eState.UPDATE:
                    {
                        TextBlock textBlock = (TextBlock)(this.Content);
                        //this.Background = new SolidColorBrush(Colors.Orchid);
                        //textBlock.Foreground = new SolidColorBrush(Colors.MediumBlue);
                        //textBlock.TextDecorations = TextDecorations.Underline;
                        ((MenuItem)this.ContextMenu.Items[0]).Header = "Update (" + string.Format("{0:0.###}", zipSize / 1024.0 / 1024.0) + " MB download)";
                        ((MenuItem)this.ContextMenu.Items[0]).IsEnabled = true; //Install
                        ((MenuItem)this.ContextMenu.Items[1]).IsEnabled = true; //Uninstall
                        ((MenuItem)this.ContextMenu.Items[2]).IsEnabled = false; //Enable-disable
                        bar.Fill = new SolidColorBrush(Colors.Orchid);
                        ((ToolTip)this.ToolTip).Content = "Click to update";
                    }
                    break;
                case eState.INSTALLED:
                    {
                        TextBlock textBlock = (TextBlock)(this.Content);
                        //this.Background = new SolidColorBrush(Colors.SpringGreen);
                        //textBlock.Foreground = new SolidColorBrush(Colors.Black);
                        //textBlock.TextDecorations = null;
                        ((MenuItem)this.ContextMenu.Items[0]).Header = "Installed and enabled";
                        ((MenuItem)this.ContextMenu.Items[0]).IsEnabled = false;
                        ((MenuItem)this.ContextMenu.Items[1]).IsEnabled = true;
                        ((MenuItem)this.ContextMenu.Items[2]).IsEnabled = true;
                        bar.Fill = new SolidColorBrush(Colors.SpringGreen);
                        ((ToolTip)this.ToolTip).Content = "Click to disable";
                    }
                    break;
                case eState.DISABLED:
                    {
                        TextBlock textBlock = (TextBlock)(this.Content);
                        //this.Background = new SolidColorBrush(Colors.Tomato);
                        //textBlock.Foreground = new SolidColorBrush(Colors.Black);
                        //textBlock.TextDecorations = null;
                        ((MenuItem)this.ContextMenu.Items[0]).Header = "Installed and disabled";
                        ((MenuItem)this.ContextMenu.Items[0]).IsEnabled = false;
                        ((MenuItem)this.ContextMenu.Items[1]).IsEnabled = true;
                        ((MenuItem)this.ContextMenu.Items[2]).IsEnabled = true;
                        bar.Fill = new SolidColorBrush(Colors.Tomato);
                        ((ToolTip)this.ToolTip).Content = "Click to enable";
                    }
                    break;
            }
        }

        public eState GetState()
        {
            return state;
        }
    }
}
