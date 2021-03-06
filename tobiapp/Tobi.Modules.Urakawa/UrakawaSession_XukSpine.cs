﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Microsoft.Practices.Composite.Logging;
using Tobi.Common;
using Tobi.Common.MVVM;
using Tobi.Common.MVVM.Command;
using Tobi.Common.UI;
using urakawa.command;
using urakawa.ExternalFiles;
using Microsoft.Practices.Unity;
using urakawa.property.xml;
using urakawa.xuk;

namespace Tobi.Plugin.Urakawa
{
    public partial class UrakawaSession
    {
        public bool IsSplitMaster
        {
            get
            {
                if (DocumentProject == null || HasXukSpine || IsXukSpine)
                {
                    return false;
                }

                XmlProperty xmlProp = DocumentProject.Presentations.Get(0).RootNode.GetXmlProperty();
                if (xmlProp == null)
                {
                    return false;
                }
                XmlAttribute xmlAttr = xmlProp.GetAttribute(SPLIT_MERGE);

                return xmlAttr != null && "MASTER".Equals(xmlAttr.Value, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool IsSplitSub
        {
            get
            {
                if (DocumentProject == null || HasXukSpine || IsXukSpine)
                {
                    return false;
                }

                XmlProperty xmlProp = DocumentProject.Presentations.Get(0).RootNode.GetXmlProperty();
                if (xmlProp == null)
                {
                    return false;
                }
                XmlAttribute xmlAttr = xmlProp.GetAttribute(SPLIT_MERGE);

                return xmlAttr != null && !"MASTER".Equals(xmlAttr.Value, StringComparison.InvariantCultureIgnoreCase) && !"-1".Equals(xmlAttr.Value, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public string XukSpineProjectPath { get; set; }

        public bool IsXukSpine
        {
            get
            {
                return !string.IsNullOrEmpty(DocumentFilePath)
                       &&
                       OpenXukAction.XUK_SPINE_EXTENSION.Equals(Path.GetExtension(DocumentFilePath), StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool HasXukSpine
        {
            get
            {
                return XukSpineItems != null && XukSpineItems.Count > 0;
            }
        }

        public RichDelegateCommand ShowXukSpineCommand { get; private set; }

        public ObservableCollection<XukSpineItemData> XukSpineItems
        {
            get;
            private set;
        }

        private void InitializeXukSpines()
        {
            ShowXukSpineCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdShowXukSpine_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdShowXukSpine_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadTangoIcon(@"preferences-desktop-locale"),
                () =>
                {
                    m_Logger.Log("UrakawaSession.ShowXukSpineCommand", Category.Debug, Priority.Medium);

                    var view = m_Container.Resolve<XukSpineView>();

                    var windowPopup = new PopupModalWindow(m_ShellView,
                                                           UserInterfaceStrings.EscapeMnemonic(
                                                           Tobi_Plugin_Urakawa_Lang.CmdShowXukSpine_ShortDesc
                        //Tobi_Plugin_Urakawa_Lang.CmdOpenRecent_ShortDesc
                                                           ),
                                                           view,
                                                           PopupModalWindow.DialogButtonsSet.OkCancel,
                                                           PopupModalWindow.DialogButton.Ok,
                                                           true, 400, 600, null, 0, null);
                    //view.OwnerWindow = windowPopup;

                    windowPopup.EnableEnterKeyDefault = true;

                    windowPopup.ShowModal();

                    if (windowPopup.ClickedDialogButton == PopupModalWindow.DialogButton.Ok)
                    {
                        if (view.XukSpineItemsList.SelectedItem != null)
                        {
                            var item = (XukSpineItemWrapper) view.XukSpineItemsList.SelectedItem;
                            string str = item.Data.Uri.IsFile ? item.Data.Uri.LocalPath : item.Data.Uri.ToString();

                            if (view.check.IsChecked.GetValueOrDefault() && item.SplitMerged)
                            {
                                string parentDir = Path.GetDirectoryName(str);
                                string fileNameWithoutExtn = Path.GetFileNameWithoutExtension(str);

                                string mergedDirName = MERGE_PREFIX + @"_" + fileNameWithoutExtn;
                                string mergedDir = Path.Combine(parentDir, mergedDirName);

                                str = Path.Combine(mergedDir, Path.GetFileName(str));
                            }

                            try
                            {
                                OpenFile(str);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHandler.Handle(ex, false, m_ShellView);
                            }
                        }
                    }
                    else if (windowPopup.ClickedDialogButton == PopupModalWindow.DialogButton.Apply)
                    {
                        // IsXukSpine ? DocumentFilePath : XukSpineProjectPath

                        bool opened = true;
                        if (!IsXukSpine)
                        {
                            opened = false;
                            try
                            {
                                opened = OpenFile(XukSpineProjectPath, false);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHandler.Handle(ex, false, m_ShellView);
                            }
                        }

                        if (opened)
                        {
                            ExportCommand.Execute();
                        }
                    }
                    else if (windowPopup.ClickedDialogButton == PopupModalWindow.DialogButton.Close)
                    {
                        // IsXukSpine ? DocumentFilePath : XukSpineProjectPath

                        bool opened = true;
                        if (!IsXukSpine)
                        {
                            opened = false;
                            try
                            {
                                opened = OpenFile(XukSpineProjectPath, false);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHandler.Handle(ex, false, m_ShellView);
                            }
                        }

                        if (opened)
                        {
                            MergeProjectCommand.Execute();
                        }
                    }
                    else if (windowPopup.ClickedDialogButton == PopupModalWindow.DialogButton.No)
                    {
                        // IsXukSpine ? DocumentFilePath : XukSpineProjectPath

                        bool opened = true;
                        if (!IsXukSpine)
                        {
                            opened = false;
                            try
                            {
                                opened = OpenFile(XukSpineProjectPath, true);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHandler.Handle(ex, false, m_ShellView);
                            }
                        }

                        //if (opened)
                        //{
                        //    ShowXukSpineCommand.Execute();
                        //}
                    }
                },
                () => HasXukSpine && !isAudioRecording,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_ShowXukSpine)
                );

            m_ShellView.RegisterRichCommand(ShowXukSpineCommand);
        }
    }
}
