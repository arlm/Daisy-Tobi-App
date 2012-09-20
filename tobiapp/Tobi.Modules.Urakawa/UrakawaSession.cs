﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AudioLib;
using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.Composite.Logging;
using Microsoft.Practices.Unity;
using Tobi.Common;
using Tobi.Common.MVVM;
using Tobi.Common.MVVM.Command;
using Tobi.Common.UI;
using Tobi.Common.Validation;
using urakawa;
using urakawa.core;
using urakawa.data;
using urakawa.events;


namespace Tobi.Plugin.Urakawa
{
    ///<summary>
    /// Single shared instance (singleton) of a session to host the Urakawa SDK aurthoring data model.
    ///</summary>
    [Export(typeof(IUrakawaSession)), PartCreationPolicy(CreationPolicy.Shared)]
    public sealed partial class UrakawaSession : PropertyChangedNotifyBase, IUrakawaSession, IPartImportsSatisfiedNotification
    {
#pragma warning disable 1591 // non-documented method
        public void OnImportsSatisfied()
#pragma warning restore 1591
        {
            //#if DEBUG
            //            Debugger.Break();
            //#endif
        }

        private readonly ILoggerFacade m_Logger;
        private readonly IEventAggregator m_EventAggregator;
        private readonly IShellView m_ShellView;
        private readonly IUnityContainer m_Container;

        [ImportMany(typeof(IValidator), RequiredCreationPolicy = CreationPolicy.Shared, AllowRecomposition = true)]
        private IEnumerable<IValidator> m_Validators;

        ///<summary>
        /// We inject a few dependencies in this constructor.
        /// No document is open and IsDirty is initialized to false.
        ///</summary>
        ///<param name="logger">normally obtained from the Unity container, it's a built-in CAG service</param>
        ///<param name="container">normally obtained from the Unity dependency injection container, it's a built-in CAG service</param>
        ///<param name="eventAggregator">normally obtained from the Unity container, it's a built-in CAG service</param>
        ///<param name="shellView">normally obtained from the Unity container, it's a Tobi-specific entity</param>
        [ImportingConstructor]
        public UrakawaSession(
            ILoggerFacade logger,
            IUnityContainer container,
            IEventAggregator eventAggregator,
            [Import(typeof(IShellView), RequiredCreationPolicy = CreationPolicy.Shared, AllowDefault = false)]
            IShellView shellView)
        {
            m_Logger = logger;
            m_Container = container;
            m_EventAggregator = eventAggregator;
            m_ShellView = shellView;

            //IsDirty = false;

            InitializeCommands();
            InitializeRecentFiles();
            InitializeXukSpines();


            Settings.Default.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == PropertyChangedNotifyBase.GetMemberName(() => Settings.Default.TextSyncGranularity))
                {
                    m_TextSyncGranularityElements = null;
                }
                else if (e.PropertyName == PropertyChangedNotifyBase.GetMemberName(() => Settings.Default.Skippables))
                {
                    m_SkippableElements = null;
                }
            };
        }

        //#pragma warning disable 1591 // missing comments

        //#pragma warning restore 1591
        //public RichDelegateCommand NewCommand { get; private set; }

        public RichDelegateCommand CloseCommand { get; private set; }

        public RichDelegateCommand UndoCommand { get; private set; }
        public RichDelegateCommand RedoCommand { get; private set; }

        public RichDelegateCommand OpenDocumentFolderCommand { get; private set; }

        public RichDelegateCommand DataCleanupCommand { get; private set; }

        private Project m_DocumentProject;
        public Project DocumentProject
        {
            get { return m_DocumentProject; }
            set
            {
                if (m_DocumentProject == value)
                {
                    return;
                }
                lock (m_TreeNodeSelectionLock)
                {
                    m_TreeNode = null;
                    m_SubTreeNode = null;
                }
                if (m_DocumentProject != null)
                {
                    //m_DocumentProject.Changed -= OnDocumentProjectChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.CommandDone -= OnUndoRedoManagerChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.CommandReDone -= OnUndoRedoManagerChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.CommandUnDone -= OnUndoRedoManagerChanged;
                    //m_DocumentProject.Presentations.Get(0).UndoRedoManager.TransactionStarted -= OnUndoRedoManagerChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.TransactionEnded -= OnUndoRedoManagerChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.TransactionCancelled -= OnUndoRedoManagerChanged;
                }

                //IsDirty = false;
                m_DocumentProject = value;
                if (m_DocumentProject != null)
                {
                    //m_DocumentProject.Changed += OnDocumentProjectChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.CommandDone += OnUndoRedoManagerChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.CommandReDone += OnUndoRedoManagerChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.CommandUnDone += OnUndoRedoManagerChanged;
                    //m_DocumentProject.Presentations.Get(0).UndoRedoManager.TransactionStarted += OnUndoRedoManagerChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.TransactionEnded += OnUndoRedoManagerChanged;
                    m_DocumentProject.Presentations.Get(0).UndoRedoManager.TransactionCancelled += OnUndoRedoManagerChanged;
                }
                RaisePropertyChanged(() => DocumentProject);
                RaisePropertyChanged(() => IsDirty);
            }
        }

        private string m_DocumentFilePath;
        [NotifyDependsOn("DocumentProject")]
        public string DocumentFilePath
        {
            get { return m_DocumentFilePath; }
            set
            {
                if (m_DocumentFilePath == value)
                {
                    return;
                }
                m_DocumentFilePath = value;
                RaisePropertyChanged(() => DocumentFilePath);
            }
        }

        public bool IsAcmCodecsDisabled
        {
            get { return Settings.Default.AudioCodecDisableACM; }
        }

        //private bool m_IsDirty;
        public bool IsDirty
        {
            get
            {
                if (m_DocumentProject != null)
                {
                    return !m_DocumentProject.Presentations.Get(0).UndoRedoManager.IsOnDirtyMarker();
                }
                return false;
                //return m_IsDirty;
            }
            //set
            //{
            //    if (m_IsDirty == value)
            //    {
            //        return;
            //    }
            //    m_IsDirty = value;
            //    RaisePropertyChanged(() => IsDirty);
            //}
        }


        private void OnUndoRedoManagerChanged(object sender, DataModelChangedEventArgs e)
        {
            RaisePropertyChanged(() => IsDirty);
            //IsDirty = m_DocumentProject.Presentations.Get(0).UndoRedoManager.CanUndo;
        }

        //private void OnDocumentProjectChanged(object sender, DataModelChangedEventArgs e)
        //{
        //    RaisePropertyChanged(() => IsDirty);
        //    //IsDirty = true;
        //}

        internal void InitializeCommands()
        {
            initCommands_Open();
            initCommands_Save();
            initCommands_Export();

            OpenDocumentFolderCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdOpenDocumentFolder_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdOpenDocumentFolder_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeFoxtrotIcon(@"Foxtrot_user-home"),
                () =>
                {
                    m_Logger.Log(@"ShellView.OpenDocumentFolderCommand", Category.Debug, Priority.Medium);

                    m_ShellView.ExecuteShellProcess(Path.GetDirectoryName(DocumentFilePath));
                },
                 () => DocumentProject != null && !string.IsNullOrEmpty(DocumentFilePath),
                Settings_KeyGestures.Default,
                null //PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_ShowTobiFolder)
                );

            m_ShellView.RegisterRichCommand(OpenDocumentFolderCommand);

            //
            //
            UndoCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdUndo_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdUndo_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeNeuIcon(@"Neu_edit-undo"),
                () => DocumentProject.Presentations.Get(0).UndoRedoManager.Undo(),
                () => DocumentProject != null && DocumentProject.Presentations.Get(0).UndoRedoManager.CanUndo,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Undo));

            m_ShellView.RegisterRichCommand(UndoCommand);
            //
            RedoCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdRedo_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdRedo_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeNeuIcon(@"Neu_edit-redo"),
                () => DocumentProject.Presentations.Get(0).UndoRedoManager.Redo(),
                () => DocumentProject != null && DocumentProject.Presentations.Get(0).UndoRedoManager.CanRedo,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Redo));

            m_ShellView.RegisterRichCommand(RedoCommand);
            //
            CloseCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdClose_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdClose_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadTangoIcon(@"emblem-symbolic-link"),
                () =>
                {
                    PopupModalWindow.DialogButton button =
                        CheckSaveDirtyAndClose(PopupModalWindow.DialogButtonsSet.YesNoCancel, "confirm");
                    //if (button != PopupModalWindow.DialogButton.Ok)
                    //{
                    //    return false;
                    //}
                },
                () => DocumentProject != null,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_CloseProject));

            m_ShellView.RegisterRichCommand(CloseCommand);
            //
            DataCleanupCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeNeuIcon(@"Neu_user-trash-full"),
                () => DataCleanup(true),
                () => DocumentProject != null
                && !IsXukSpine,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_DataCleanup));

            m_ShellView.RegisterRichCommand(DataCleanupCommand);
        }

        public string DataCleanup(bool interactive)
        {
            // Backup before close.
            string docPath = DocumentFilePath;
            Project project = DocumentProject;

            if (interactive)
            {
                // Closing is REQUIRED ! 
                PopupModalWindow.DialogButton button = CheckSaveDirtyAndClose(
                    PopupModalWindow.DialogButtonsSet.OkCancel, "data cleanup");
                if (!PopupModalWindow.IsButtonOkYesApply(button))
                {
                    return null;
                }
            }

            project.Presentations.Get(0).UndoRedoManager.FlushCommands();
            //RaisePropertyChanged(()=>IsDirty);

            // ==> SAVED AND CLOSED (clipboard removed), undo-redo removed.

            var dataFolderPath = project.Presentations.Get(0).DataProviderManager.DataFileDirectoryFullPath;

            var deletedDataFolderPath = Path.Combine(dataFolderPath, "__DELETED" + Path.DirectorySeparatorChar);
            if (!Directory.Exists(deletedDataFolderPath))
            {
                FileDataProvider.CreateDirectory(deletedDataFolderPath);
            }

            bool cancelled = false;

            if (interactive)
            {
                bool result = m_ShellView.RunModalCancellableProgressTask(true,
                                                                          Tobi_Plugin_Urakawa_Lang.CleaningUpDataFiles,
                                                                          new Cleaner(project.Presentations.Get(0),
                                                                                      deletedDataFolderPath),
                    //project.Presentations.Get(0).Cleanup();
                                                                          () =>
                                                                          {
                                                                              m_Logger.Log(@"CANCELLED",
                                                                                           Category.Debug,
                                                                                           Priority.Medium);
                                                                              cancelled = true;

                                                                          },
                                                                          () =>
                                                                          {
                                                                              m_Logger.Log(@"DONE", Category.Debug,
                                                                                           Priority.Medium);
                                                                              cancelled = false;

                                                                          });
                if (cancelled)
                {
                    DebugFix.Assert(!result);
                }
            }
            else
            {
                var cleaner = new Cleaner(project.Presentations.Get(0),
                                          deletedDataFolderPath);
                //cleaner.DoWork();
                cleaner.Cleanup();
            }

            if (cancelled)
            {
                // We restore the old one, not cleaned-up (or partially...).

                DocumentFilePath = null;
                DocumentProject = null;

                try
                {
                    OpenFile(docPath);
                }
                catch (Exception ex)
                {
                    ExceptionHandler.Handle(ex, false, m_ShellView);
                }
            }
            else
            {
                var listOfDataProviderFiles = new List<string>();
                foreach (var dataProvider in project.Presentations.Get(0).DataProviderManager.ManagedObjects.ContentsAs_Enumerable)
                {
                    var fileDataProvider = dataProvider as FileDataProvider;
                    if (fileDataProvider == null) continue;

                    listOfDataProviderFiles.Add(fileDataProvider.DataFileRelativePath);
                }


                bool folderIsShowing = false;

                if (interactive)
                {
                    if (Directory.GetFiles(deletedDataFolderPath).Length != 0)
                    {
                        folderIsShowing = true;

                        m_ShellView.ExecuteShellProcess(deletedDataFolderPath);
                    }
                }

                foreach (string filePath in Directory.GetFiles(dataFolderPath))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (!listOfDataProviderFiles.Contains(fileName))
                    {
                        var filePathDest = Path.Combine(deletedDataFolderPath, fileName);
                        DebugFix.Assert(!File.Exists(filePathDest));
                        if (!File.Exists(filePathDest))
                        {
                            File.Move(filePath, filePathDest);
                        }
                    }
                }

                if (interactive)
                {
                    if (!folderIsShowing && Directory.GetFiles(deletedDataFolderPath).Length != 0)
                    {
                        m_ShellView.ExecuteShellProcess(deletedDataFolderPath);
                    }

                    // We must now save the modified cleaned-up doc

                    DocumentFilePath = docPath;
                    DocumentProject = project;

                    if (save())
                    {
                        DocumentFilePath = null;
                        DocumentProject = null;

                        try
                        {
                            OpenFile(docPath);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHandler.Handle(ex, false, m_ShellView);
                        }
                    }
                }
            }

            return deletedDataFolderPath;

            //if (!Dispatcher.CurrentDispatcher.CheckAccess())
            //{
            //Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(RefreshUI_WaveFormChunkMarkers));
            //    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, (Action) (() =>
            //      {
            //          var p = new Process
            //          {
            //              StartInfo = { FileName = deletedDataFolderPath }
            //          };
            //          p.Start();
            //      }));
            //}
            //else
            //{
            //    //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            //    //{
            //    //    var p = new Process
            //    //    {
            //    //        StartInfo = { FileName = deletedDataFolderPath }
            //    //    };
            //    //    p.Start();
            //    //}));

            //    //var p = new Process
            //    //{
            //    //    StartInfo = { FileName = deletedDataFolderPath }
            //    //};
            //    //p.Start();


            //    Thread thread = new Thread(new ThreadStart(() =>
            //    {
            //        var p = new Process
            //        {
            //            StartInfo =
            //                {
            //                    FileName = deletedDataFolderPath,
            //                    CreateNoWindow = true,
            //                    UseShellExecute = true,
            //                    WorkingDirectory = deletedDataFolderPath
            //                }
            //        };
            //        p.Start();
            //    }));
            //    thread.Name = "File explorer popup from Tobi (after cleanup _DELETED)";
            //    thread.Priority = ThreadPriority.Normal;
            //    thread.IsBackground = true;
            //    thread.Start();
            //}

        }

        public PopupModalWindow.DialogButton CheckSaveDirtyAndClose(PopupModalWindow.DialogButtonsSet buttonset, string role)
        {
            if (m_ShellView != null)
            {
                m_ShellView.RaiseEscapeEvent();
            }

            if (DocumentProject == null)
            {
                return PopupModalWindow.DialogButton.Ok;
            }

            var result = PopupModalWindow.DialogButton.Ok;

            if (IsDirty)
            {
                m_Logger.Log(@"UrakawaSession.askUserSave", Category.Debug, Priority.Medium);

                var label = new TextBlock //TextBoxReadOnlyCaretVisible(Tobi_Plugin_Urakawa_Lang.UnsavedChangesConfirm) //
                {
                    Text = Tobi_Plugin_Urakawa_Lang.UnsavedChangesConfirm,
                    Margin = new Thickness(8, 0, 8, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Focusable = true,
                    TextWrapping = TextWrapping.Wrap
                };

                var iconProvider = new ScalableGreyableImageProvider(m_ShellView.LoadTangoIcon(@"help-browser"),
                                                                     m_ShellView.MagnificationLevel);

                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };
                panel.Children.Add(iconProvider.IconLarge);
                panel.Children.Add(label);
                //panel.Margin = new Thickness(8, 8, 8, 0);

                var details = new TextBoxReadOnlyCaretVisible
                    {
                        FocusVisualStyle = (Style)Application.Current.Resources["MyFocusVisualStyle"],

                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(6),
                        TextReadOnly = Tobi_Plugin_Urakawa_Lang.UnsavedChangesDetails
                    };

                var windowPopup = new PopupModalWindow(m_ShellView,
                                                       UserInterfaceStrings.EscapeMnemonic(Tobi_Plugin_Urakawa_Lang.UnsavedChanges) + (string.IsNullOrEmpty(role) ? "" : " (" + role + ")"),
                                                       panel,
                                                       buttonset,
                                                       PopupModalWindow.DialogButton.Cancel,
                                                       buttonset == PopupModalWindow.DialogButtonsSet.Cancel
                                                       || buttonset == PopupModalWindow.DialogButtonsSet.OkCancel
                                                       || buttonset == PopupModalWindow.DialogButtonsSet.YesNoCancel,
                                                       350, 180, details, 40, null);

                windowPopup.ShowModal();

                if (PopupModalWindow.IsButtonEscCancel(windowPopup.ClickedDialogButton))
                {
                    return PopupModalWindow.DialogButton.Cancel;
                }

                if (PopupModalWindow.IsButtonOkYesApply(windowPopup.ClickedDialogButton))
                {
                    if (!save())
                    {
                        return PopupModalWindow.DialogButton.Cancel;
                    }

                    result = PopupModalWindow.DialogButton.Ok;
                }
                else
                {
                    result = PopupModalWindow.DialogButton.No;
                }
            }

            //m_Logger.Log(@"-- PublishEvent [ProjectUnLoadedEvent] UrakawaSession.close", Category.Debug, Priority.Medium);

            var oldProject = DocumentProject;

            XukSpineItems = null; // new ObservableCollection<Uri>();

            DocumentFilePath = null;
            DocumentProject = null;

            if (m_EventAggregator != null)
            {
                m_EventAggregator.GetEvent<ProjectUnLoadedEvent>().Publish(oldProject);
            }

            return result;
        }

        private List<string> m_SkippableElements;
        private bool isElementSkippable(string name)
        {
            if (m_SkippableElements == null)
            {
                string[] names = Settings.Default.Skippables.Split(new char[] { ',', ' ', ';', '/' });

                //m_SkippableElements = new List<string>(names);
                m_SkippableElements = new List<string>(names.Length);

                foreach (string n in names)
                {
                    string n_ = n.Trim(); //.ToLower();
                    if (!string.IsNullOrEmpty(n_))
                    {
                        m_SkippableElements.Add(n_);
                    }
                }
            }

            foreach (var str in m_SkippableElements)
            {
                if (str.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;

            //return m_SkippableElements.Contains(name.ToLower());
        }

        public bool isTreeNodeSkippable(TreeNode node)
        {
            if (node.HasXmlProperty && isElementSkippable(node.GetXmlElementLocalName()))
            {
                return true;
            }
            if (node.Parent == null)
            {
                return false;
            }
            return isTreeNodeSkippable(node.Parent);
        }


        private List<string> m_TextSyncGranularityElements;
        public TreeNode AdjustTextSyncGranularity(TreeNode node, TreeNode upperLimit)
        {
            if (//!Settings.Default.EnableTextSyncGranularity ||
                node == null || !node.HasXmlProperty)
            {
                return null;
            }

            if (m_TextSyncGranularityElements == null)
            {
                string[] names = Settings.Default.TextSyncGranularity.Split(new char[] { ',', ' ', ';', '/' });

                //m_SkippableElements = new List<string>(names);
                m_TextSyncGranularityElements = new List<string>(names.Length);

                foreach (string n in names)
                {
                    string n_ = n.Trim(); //.ToLower();
                    if (!string.IsNullOrEmpty(n_))
                    {
                        m_TextSyncGranularityElements.Add(n_);
                    }
                }
            }

            if (m_TextSyncGranularityElements.Count > 0 && m_TextSyncGranularityElements[0] == "*")
            {
                return null;
            }

            foreach (var str in m_TextSyncGranularityElements)
            {
                TreeNode parent = node; //.Parent;
                while (parent != null)
                {
                    if (parent.IsAncestorOf(upperLimit))
                    {
                        break;
                    }

                    string name = parent.GetXmlElementLocalName();
                    if (str.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        return parent;
                    }

                    parent = parent.Parent;
                }
            }

            return null;
        }
    }
}
