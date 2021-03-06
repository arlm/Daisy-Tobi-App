﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AudioLib;
using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.Composite.Logging;
using Microsoft.Practices.Unity;
using PipelineWSClient;
using Tobi.Common;
using Tobi.Common.MVVM;
using Tobi.Common.MVVM.Command;
using Tobi.Common.UI;
using Tobi.Common.Validation;
using urakawa;
using urakawa.core;
using urakawa.data;
using urakawa.events;
using urakawa.undo;
using urakawa.xuk;
using urakawa.command;
using urakawa.events.undo;
using urakawa.ExternalFiles;


namespace Tobi.Plugin.Urakawa
{
    public enum JavaXmx : ushort
    {
        _1G = 1024,
        _512M = 512
    }

    ///<summary>
    /// Single shared instance (singleton) of a session to host the Urakawa SDK aurthoring data model.
    ///</summary>
    [Export(typeof(IUrakawaSession)), PartCreationPolicy(CreationPolicy.Shared)]
    public sealed partial class UrakawaSession : PropertyChangedNotifyBase, IUrakawaSession, IPartImportsSatisfiedNotification, UndoRedoManager.Hooker.Host
    {
#pragma warning disable 1591 // non-documented method
        public void OnImportsSatisfied()
#pragma warning restore 1591
        {
            //#if DEBUG
            //            Debugger.Break();
            //#endif
        }

        //public bool PerformanceFlag
        //{
        //    get; set;
        //}

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

            Tobi.Common.Settings.Default.PropertyChanged += OnSettingsPropertyChanged;
            Settings.Default.PropertyChanged += OnSettingsPropertyChanged;

            //m_EventAggregator.GetEvent<OpenFileRequestEvent>().Subscribe((string path) => TryOpenFile(path));
        }

        private bool m_PipelineWasStarted = false;
        ~UrakawaSession()
        {
            Tobi.Common.Settings.Default.PropertyChanged -= OnSettingsPropertyChanged;
            Settings.Default.PropertyChanged -= OnSettingsPropertyChanged;

            if (m_PipelineWasStarted)
            {
                try
                {
                    Resources.Halt();
                }
                catch (Exception ex)
                {
#if DEBUG
                    Debugger.Break();
#endif
                    m_Logger.Log(
                        String.Format(@"Pipeline2 server not halted! ({0})", ex.Message),
                        Category.Debug, Priority.Medium);
                }
            }
        }

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == PropertyChangedNotifyBase.GetMemberName(() => Tobi.Common.Settings.Default.TextAudioSyncGranularity)
                //|| e.PropertyName == PropertyChangedNotifyBase.GetMemberName(() => Tobi.Common.Settings.Default.EnableTextSyncGranularity)
                )
            {
                m_TextSyncGranularityElements = null;
            }
            else if (e.PropertyName == PropertyChangedNotifyBase.GetMemberName(() => Settings.Default.Skippables))
            {
                m_SkippableElements = null;
            }
        }

        //#pragma warning disable 1591 // missing comments

        //#pragma warning restore 1591
        //public RichDelegateCommand NewCommand { get; private set; }

        public RichDelegateCommand CloseCommand { get; private set; }

        public RichDelegateCommand UndoCommand { get; private set; }
        public RichDelegateCommand RedoCommand { get; private set; }

        public RichDelegateCommand OpenDocumentFolderCommand { get; private set; }

        public RichDelegateCommand DataCleanupStrictCommand { get; private set; }
        public RichDelegateCommand DataCleanupOptimizeCommand { get; private set; }
        public RichDelegateCommand DataCleanupRollbackCommand { get; private set; }

        public bool isAudioActive
        {
            get { return isAudioRecording || isAudioPlaying || isAudioMonitoring; }
        }

        private bool m_isAudioRecording = false;
        public bool isAudioRecording
        {
            get { return m_isAudioRecording; }
            set
            {
                m_isAudioRecording = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool m_isAudioPlaying = false;
        public bool isAudioPlaying
        {
            get { return m_isAudioPlaying; }
            set
            {
                m_isAudioPlaying = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool m_isAudioMonitoring = false;
        public bool isAudioMonitoring
        {
            get { return m_isAudioMonitoring; }
            set
            {
                m_isAudioMonitoring = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private UndoRedoManager.Hooker m_UndoRedoManagerHooker = null;

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
                if (m_UndoRedoManagerHooker != null)
                {
                    if (m_UndoRedoManagerHooker != null) m_UndoRedoManagerHooker.UnHook();
                    m_UndoRedoManagerHooker = null;
                }

                //IsDirty = false;
                m_DocumentProject = value;
                if (m_DocumentProject != null)
                {
                    m_UndoRedoManagerHooker = m_DocumentProject.Presentations.Get(0).UndoRedoManager.Hook(this);
                }
                RaisePropertyChanged(() => DocumentProject);
                RaisePropertyChanged(() => IsDirty);

                //if (m_DocumentProject == null)
                //{
                //    // XukStrings maintains a pointer to the last-created Project instance!
                //    XukStrings.NullifyProjectReference();
                //}
                //else
                //{
                //    // XukStrings maintains a pointer to the last-created Project instance!
                //    XukStrings.RelocateProjectReference(m_DocumentProject);
                //}
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

        private bool m_AutoSave_OFF = false;
        public void AutoSave_OFF()
        {
            m_AutoSave_OFF = true;
        }
        public void AutoSave_ON()
        {
            m_AutoSave_OFF = false;
        }

        private DispatcherTimer m_undoAutoSaveIntervalTimer = null;

        public void OnUndoRedoManagerChanged(UndoRedoManagerEventArgs eventt, bool done, Command command, bool isTransactionEndEvent, bool isNoTransactionOrTrailingEdge)
        {
            //            if (!Dispatcher.CheckAccess())
            //            {
            //#if DEBUG
            //                Debugger.Break();
            //#endif

            //#if NET40x
            //                TheDispatcher.Invoke(DispatcherPriority.Normal,
            //                    (Action<UndoRedoManagerEventArgs, bool, Command, bool, bool>)OnUndoRedoManagerChanged,
            //                    eventt, done, command, isTransactionEndEvent, isNoTransactionOrTrailingEdge);
            //#else
            //            TheDispatcher.Invoke(DispatcherPriority.Normal,
            //                (Action)(() => OnUndoRedoManagerChanged(eventt, done, command, isTransactionEndEvent, isNoTransactionOrTrailingEdge))
            //                );
            //#endif
            //                return;
            //            }

            if (command is CompositeCommand)
            {
#if DEBUG
                Debugger.Break();
#endif
            }

            if (isNoTransactionOrTrailingEdge)
            {
                if (m_undoAutoSaveIntervalTimer == null)
                {
                    m_undoAutoSaveIntervalTimer = new DispatcherTimer(DispatcherPriority.Background);
                    m_undoAutoSaveIntervalTimer.Interval = TimeSpan.FromMilliseconds(1000);
                    m_undoAutoSaveIntervalTimer.Tick += (oo, ee) =>
                    {
                        m_undoAutoSaveIntervalTimer.Stop();
                        //m_scrollRefreshIntervalTimer = null;

                        if (!m_AutoSave_OFF
                            && Settings.Default.EnableAutoSave
                            && !string.IsNullOrEmpty(DocumentFilePath))
                        {
                            // The "OnUndoRedoManagerChanged" event is not broadcasted when 
                            // Command.Execute() or Command.UnExecute() fails,
                            // so we never auto-save a corrupted project.
                            try
                            {
                                saveAuto();
                            }
                            catch (Exception ex)
                            {
#if DEBUG
                                Debugger.Break();
#endif
                                Console.WriteLine(ex.Message);
                                Console.WriteLine(ex.StackTrace);
                            }

                            //Application.Current.MainWindow.Dispatcher.BeginInvoke(
                            //DispatcherPriority.Background,
                            //(Action)(() =>
                            //{
                            ///// SAVE HERE
                            //}));
                        }
                    };
                    m_undoAutoSaveIntervalTimer.Start();
                }
                else if (m_undoAutoSaveIntervalTimer.IsEnabled)
                {
                    //restart
                    m_undoAutoSaveIntervalTimer.Stop();
                    m_undoAutoSaveIntervalTimer.Start();
                }
                else
                {
                    m_undoAutoSaveIntervalTimer.Start();
                }

                RaisePropertyChanged(() => IsDirty);
                //IsDirty = m_DocumentProject.Presentations.Get(0).UndoRedoManager.CanUndo;
            }
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
            initCommands_SplitMerge();

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
                 () => DocumentProject != null && !string.IsNullOrEmpty(DocumentFilePath) && !isAudioRecording,
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
                () => DocumentProject != null && DocumentProject.Presentations.Get(0).UndoRedoManager.CanUndo && !isAudioRecording,
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
                () => DocumentProject != null && DocumentProject.Presentations.Get(0).UndoRedoManager.CanRedo && !isAudioRecording,
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
                () => DocumentProject != null && !isAudioRecording,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_CloseProject));

            m_ShellView.RegisterRichCommand(CloseCommand);
            //
            DataCleanupStrictCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_STRICT_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_STRICT_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeNeuIcon(@"Neu_user-trash-full"),
                () => DataCleanup(true, false),
                () => DocumentProject != null && !isAudioRecording
                && !IsXukSpine,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_DataCleanupStrict)
                );

            m_ShellView.RegisterRichCommand(DataCleanupStrictCommand);
            //
            DataCleanupOptimizeCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_OPTIMIZED_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_OPTIMIZED_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeNeuIcon(@"Neu_user-trash-full"),
                () => DataCleanup(true, true),
                () => DataCleanupStrictCommand.CanExecute(),
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_DataCleanupOptimize)
                );

            m_ShellView.RegisterRichCommand(DataCleanupOptimizeCommand);
            //
            DataCleanupRollbackCommand = new RichDelegateCommand(
                Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_ROLLBACK_ShortDesc,
                Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_ROLLBACK_LongDesc,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeNeuIcon(@"Neu_user-trash-full"),
                () => DataCleanupRollback(),
                () => DataCleanupStrictCommand.CanExecute(),
                Settings_KeyGestures.Default,
                null //PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_DataCleanupRollback)
                );

            m_ShellView.RegisterRichCommand(DataCleanupRollbackCommand);
        }

        public static readonly string CLEANUP_ROLLBACK_XUK_BACKUP_FILENAME = "TOBI_cleanup_rollback.xuk";
        public static readonly string CLEANUP_ROLLBACK_DATA_FILENAME = "TOBI_cleanup_rollback.txt";

        public void DataCleanupRollback()
        {

            if (!askUserAlt(
                UserInterfaceStrings.EscapeMnemonic(Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_ROLLBACK_ShortDesc) + @"?",
                //Tobi_Plugin_Urakawa_Lang.CmdDataCleanup_ROLLBACK_LongDesc
                Tobi_Plugin_Urakawa_Lang.CleanupRollbackWarning
                )
                )
            {
                return;
            }

            string docPath = DocumentFilePath;
            Project project = DocumentProject;

            // Closing is REQUIRED ! 
            PopupModalWindow.DialogButton button = CheckSaveDirtyAndClose(
                PopupModalWindow.DialogButtonsSet.OkCancel, "data cleanup rollback");
            if (!PopupModalWindow.IsButtonOkYesApply(button))
            {
                return;
            }

            //// XukStrings maintains a pointer to the last-created Project instance!
            //// (which was closed / reset to null with CheckSaveDirtyAndClose)
            //XukStrings.RelocateProjectReference(project);

            project.Presentations.Get(0).UndoRedoManager.FlushCommands();
            //RaisePropertyChanged(()=>IsDirty);

            // ==> SAVED AND CLOSED (clipboard removed), undo-redo removed.

            var dataFolderPath = project.Presentations.Get(0).DataProviderManager.DataFileDirectoryFullPath;

            const string DELETED = "__DELETED";

            var deletedDataFolderPath = Path.Combine(dataFolderPath, DELETED + Path.DirectorySeparatorChar);

            var cleanupRollbackXukBackupFilePath = Path.Combine(deletedDataFolderPath, CLEANUP_ROLLBACK_XUK_BACKUP_FILENAME);
            var cleanupRollbackDataFilePath = Path.Combine(deletedDataFolderPath, CLEANUP_ROLLBACK_DATA_FILENAME);

            if (//!Directory.Exists(deletedDataFolderPath)
                //|| (Directory.GetFiles(deletedDataFolderPath).Length == 0 && Directory.GetDirectories(deletedDataFolderPath).Length == 0)
                !File.Exists(cleanupRollbackXukBackupFilePath)
                || !File.Exists(cleanupRollbackDataFilePath)
                )
            {
                messageBoxAlert(Tobi_Plugin_Urakawa_Lang.CleanupRollbackNoDeletedFiles, null);

                DocumentFilePath = null;
                DocumentProject = null;
                //XukStrings.NullifyProjectReference();

                try
                {
                    OpenFile(docPath);
                }
                catch (Exception ex)
                {
                    ExceptionHandler.Handle(ex, false, m_ShellView);
                }

                return;
            }

            var docPath_ = docPath.Replace(".xuk", "_CLEANUP_ROLLBACKED.xuk");
            if (File.Exists(docPath_))
            {
                File.Delete(docPath_);
            }
            File.Move(docPath, docPath_);
            try
            {
                File.SetAttributes(docPath_, FileAttributes.Normal);
            }
            catch
            {
            }

            File.Move(cleanupRollbackXukBackupFilePath, docPath);
            try
            {
                File.SetAttributes(docPath, FileAttributes.Normal);
            }
            catch
            {
            }


            var deletedFiles = Directory.GetFiles(deletedDataFolderPath);
            //var deletedFolders = Directory.GetDirectories(deletedDataFolderPath);

            if (deletedFiles.Length != 0)
            {
                foreach (string filePath in deletedFiles)
                {
                    var fileName = Path.GetFileName(filePath);

                    if (fileName.Equals(CLEANUP_ROLLBACK_XUK_BACKUP_FILENAME, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (fileName.Equals(CLEANUP_ROLLBACK_DATA_FILENAME, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var filePathDest = Path.Combine(dataFolderPath, fileName);
                    DebugFix.Assert(!File.Exists(filePathDest));
                    if (!File.Exists(filePathDest))
                    {
                        try
                        {
                            File.Move(filePath, filePathDest);
                            File.SetAttributes(filePathDest, FileAttributes.Normal);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            StreamReader streamReader = new StreamReader(cleanupRollbackDataFilePath, Encoding.UTF8);
            try
            {
                string line = null;
                string originalFileName = null;
                
                while ((line = streamReader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (String.IsNullOrEmpty(originalFileName))
                    {
                        originalFileName = line;
                    }
                    else
                    {
                        string newFileName = line;

                        var filePathSource = Path.Combine(dataFolderPath, newFileName);
                        DebugFix.Assert(File.Exists(filePathSource));

                        var filePathDest = Path.Combine(dataFolderPath, originalFileName);
                        DebugFix.Assert(!File.Exists(filePathDest));

                        try
                        {
                            File.Move(filePathSource, filePathDest);
                            File.SetAttributes(filePathDest, FileAttributes.Normal);
                        }
                        catch
                        {
                        }

                        originalFileName = null;
                    }
                }
            }
            finally
            {
                streamReader.Close();
            }
            //File.Delete(cleanupRollbackDataFilePath);

            String randomStr = Path.GetRandomFileName().Replace('.','_');
            var cleanupRollbackDataFilePath_ = Path.Combine(dataFolderPath, randomStr + "_" + CLEANUP_ROLLBACK_DATA_FILENAME);
            if (File.Exists(cleanupRollbackDataFilePath_))
            {
                File.Delete(cleanupRollbackDataFilePath_);
            }

            try
            {
                File.Move(cleanupRollbackDataFilePath, cleanupRollbackDataFilePath_);
                File.SetAttributes(cleanupRollbackDataFilePath_, FileAttributes.Normal);
            }
            catch
            {
            }

            // deleted data folder should be totally empty
            m_ShellView.ExecuteShellProcess(deletedDataFolderPath);


            DocumentFilePath = null;
            DocumentProject = null;
            //XukStrings.NullifyProjectReference();

            try
            {
                OpenFile(docPath);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex, false, m_ShellView);
            }
        }

        public string DataCleanup(bool interactive, bool cleanAudioOptimizeFiles)
        {
            // Backup before close.
            string docPath = DocumentFilePath;
            Project project = DocumentProject;

            if (!interactive)
            {
                DebugFix.Assert(IsSplitMaster || IsSplitSub || string.IsNullOrEmpty(docPath));
            }

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

            //// XukStrings maintains a pointer to the last-created Project instance!
            //// (which was closed / reset to null with CheckSaveDirtyAndClose)
            //XukStrings.RelocateProjectReference(project);

            project.Presentations.Get(0).UndoRedoManager.FlushCommands();
            //RaisePropertyChanged(()=>IsDirty);

            // ==> SAVED AND CLOSED (clipboard removed), undo-redo removed.

            var dataFolderPath = project.Presentations.Get(0).DataProviderManager.DataFileDirectoryFullPath;

            const string DELETED = "__DELETED";

            var deletedDataFolderPath = Path.Combine(dataFolderPath, DELETED + Path.DirectorySeparatorChar);
            if (!Directory.Exists(deletedDataFolderPath))
            {
                FileDataProvider.CreateDirectory(deletedDataFolderPath);
            }

            var cleanupRollbackXukBackupFilePath = Path.Combine(deletedDataFolderPath, CLEANUP_ROLLBACK_XUK_BACKUP_FILENAME);
            var cleanupRollbackDataFilePath = Path.Combine(deletedDataFolderPath, CLEANUP_ROLLBACK_DATA_FILENAME);

            var createRollbackData = (interactive && !string.IsNullOrEmpty(docPath));
            if (createRollbackData)
            {
                if (File.Exists(cleanupRollbackXukBackupFilePath))
                {
                    File.Delete(cleanupRollbackXukBackupFilePath);
                }
                File.Copy(docPath, cleanupRollbackXukBackupFilePath);



                //if (File.Exists(cleanupRollbackDataFilePath))
                //{
                //    File.Delete(cleanupRollbackDataFilePath);
                //}
                StreamWriter streamWriter = new StreamWriter(cleanupRollbackDataFilePath, false, Encoding.UTF8);
                try
                {
                    string line = "";
                    streamWriter.WriteLine(line);
                }
                finally
                {
                    streamWriter.Close();
                }
            }

            double cleanAudioMaxFileMegaBytes = Settings.Default.CleanAudioMaxMegaBytes;

            bool cancelled = false;

            Cleaner cleaner = null;

            if (interactive)
            {
                cleaner = new Cleaner(project.Presentations.Get(0),
                    deletedDataFolderPath, cleanAudioMaxFileMegaBytes,
                    cleanAudioOptimizeFiles);
                bool error = m_ShellView.RunModalCancellableProgressTask(true,
                                                                          Tobi_Plugin_Urakawa_Lang.CleaningUpDataFiles,
                                                                          cleaner,
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
                    //DebugFix.Assert(!result);
                }
            }
            else
            {
                cleaner = new Cleaner(project.Presentations.Get(0),
                                          deletedDataFolderPath, cleanAudioMaxFileMegaBytes, cleanAudioOptimizeFiles);
                //cleaner.DoWork();
                cleaner.Cleanup();
            }

            if (createRollbackData)
            {
                StreamWriter streamWriter = new StreamWriter(cleanupRollbackDataFilePath, false, Encoding.UTF8);
                try
                {
                    List<Cleaner.OriginalRenamedFilenameTuple> listOfRenamings = cleaner.GetListOfRenamedFiles();
                    foreach (Cleaner.OriginalRenamedFilenameTuple renamedFile in listOfRenamings)
                    {
                        streamWriter.WriteLine(renamedFile.m_original);
                        streamWriter.WriteLine(renamedFile.m_renamed);
                        streamWriter.WriteLine("");
                        streamWriter.WriteLine("");
                    }
                }
                finally
                {
                    streamWriter.Close();
                }
            }

            if (cancelled)
            {
                if (createRollbackData)
                {
                    DataCleanupRollback();
                }
                else
                {
                    // We restore the old one, not cleaned-up (or partially...).

                    var deletedFiles = Directory.GetFiles(deletedDataFolderPath);
                    var deletedFolders = Directory.GetDirectories(deletedDataFolderPath);

                    if (deletedFiles.Length != 0 || deletedFolders.Length != 0)
                    {
                        foreach (string filePath in deletedFiles)
                        {
                            var fileName = Path.GetFileName(filePath);

                            if (fileName.Equals(CLEANUP_ROLLBACK_XUK_BACKUP_FILENAME, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            if (fileName.Equals(CLEANUP_ROLLBACK_DATA_FILENAME, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            var filePathDest = Path.Combine(dataFolderPath, fileName);
                            DebugFix.Assert(!File.Exists(filePathDest));
                            if (!File.Exists(filePathDest))
                            {
                                try
                                {
                                    File.Move(filePath, filePathDest);
                                    File.SetAttributes(filePathDest, FileAttributes.Normal);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }

                    m_ShellView.ExecuteShellProcess(deletedDataFolderPath);

                    DocumentFilePath = null;
                    DocumentProject = null;
                    //XukStrings.NullifyProjectReference();

                    try
                    {
                        OpenFile(docPath);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler.Handle(ex, false, m_ShellView);
                    }
                }

                return deletedDataFolderPath;
            }

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
                if (Directory.GetFiles(deletedDataFolderPath).Length != 0 || Directory.GetDirectories(deletedDataFolderPath).Length != 0)
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
                        try
                        {
                            File.SetAttributes(filePathDest, FileAttributes.Normal);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(docPath))
            {
                string projectDirRoot = Path.GetDirectoryName(docPath);
                string projectFileName = Path.GetFileName(docPath);

                foreach (string filePath in Directory.GetFiles(projectDirRoot))
                {
                    var fileName = Path.GetFileName(filePath);

                    if (projectFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (fileName.StartsWith(projectFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        var filePathDest = Path.Combine(deletedDataFolderPath, fileName);

                        if (File.Exists(filePathDest))
                        {
                            File.Delete(filePathDest);
                        }
                        File.Move(filePath, filePathDest);
                        try
                        {
                            File.SetAttributes(filePathDest, FileAttributes.Normal);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            string normalised_deletedDataFolderPath = FileDataProvider.NormaliseFullFilePath(deletedDataFolderPath);

            foreach (string dirPath in Directory.GetDirectories(dataFolderPath
                //, "*", SearchOption.TopDirectoryOnly
                ))
            {
                string normalised_dirPath = FileDataProvider.NormaliseFullFilePath(dirPath);

                if (normalised_dirPath == normalised_deletedDataFolderPath
                    || dirPath.EndsWith(DELETED))
                {
                    continue;
                }
                var dirPathDest = Path.Combine(deletedDataFolderPath, Path.GetFileName(dirPath));
                DebugFix.Assert(!File.Exists(dirPathDest));
                if (!File.Exists(dirPathDest))
                {
                    Directory.Move(normalised_dirPath.Replace('/', '\\'), dirPathDest);
                }

                //try
                //{
                //    FileDataProvider.DeleteDirectory(dirPath);
                //}
                //catch
                //{
                //    m_Logger.Log(@"FileDataProvider.DeleteDirectory!!" + dirPath, Category.Debug, Priority.Medium);
                //}
            }

            if (interactive)
            {
                if (!folderIsShowing && (Directory.GetFiles(deletedDataFolderPath).Length != 0 || Directory.GetDirectories(deletedDataFolderPath).Length != 0))
                {
                    m_ShellView.ExecuteShellProcess(deletedDataFolderPath);
                }

                // We must now save the modified cleaned-up doc

                DocumentFilePath = docPath;
                DocumentProject = project;
                //XukStrings.RelocateProjectReference(DocumentProject);

                if (save(false))
                {
                    DocumentFilePath = null;
                    DocumentProject = null;
                    //XukStrings.NullifyProjectReference();

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
                    if (!save(false))
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
            if (!Tobi.Common.Settings.Default.EnableTextSyncGranularity
                || node == null
                || !node.HasXmlProperty
                //|| node.GetManagedAudioMedia() != null
                )
            {
                return null;
            }

            if (m_TextSyncGranularityElements == null)
            {
                string[] names = Tobi.Common.Settings.Default.TextAudioSyncGranularity.Split(new char[] { ',', ' ', ';', '/' });

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
                        int parentRank = -1;
                        for (int i = 0; i < m_TextSyncGranularityElements.Count; i++)
                        {
                            if (m_TextSyncGranularityElements[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                            {
                                parentRank = i;
                                break;
                            }
                        }

                        TreeNode withText = parent.GetFirstDescendantWithText();
                        while (withText != null && withText.IsDescendantOf(parent))
                        {
                            if (!TreeNode.TextOnlyContainsPunctuation(withText.GetText()))
                            {
                                //TreeNode adjusted = TreeNode.EnsureTreeNodeHasNoSignificantTextOnlySiblings(false, parent, withText);
                                TreeNode adjusted = TreeNode.AdjustSignificantTextOnlyAncestors(withText);
                                if (adjusted != null
                                    && adjusted.IsDescendantOf(parent)
                                    //&& adjusted != parent
                                    && adjusted.HasXmlProperty)
                                {
                                    string name_ = adjusted.GetXmlElementLocalName();
                                    int nodeRank = -1;
                                    for (int i = 0; i < m_TextSyncGranularityElements.Count; i++)
                                    {
                                        if (m_TextSyncGranularityElements[i].Equals(name_,
                                                                                    StringComparison.OrdinalIgnoreCase))
                                        {
                                            nodeRank = i;
                                            break;
                                        }
                                    }


                                    if (nodeRank >= 0 && nodeRank <= parentRank)
                                    {
                                        return null;
                                    }

                                    withText = adjusted;
                                }
                            }

                            withText = withText.GetNextSiblingWithText();
                        }

                        if (parent != null
                            &&
                            (parent.GetFirstDescendantWithXmlElement("noteref") != null
                            || parent.GetFirstDescendantWithXmlElement("annoref") != null
                            )
                            )
                        {
                            // veto adjustement because risk of killing noteref in SMIL sync
                            return node;
                        }
                        return parent;
                    }

                    parent = parent.Parent;
                }
            }

            return null;
        }

        private bool checkWarningFilePathLength(string filePath)
        {
            if (filePath.Length <= Settings.Default.FilePathMax)
            {
                return false;
            }

            m_Logger.Log("UrakawaSession warningFilePathLength", Category.Debug, Priority.Medium);

            var label = new TextBlock
            {
                Text = String.Format(Tobi_Plugin_Urakawa_Lang.WarningFilePathLength, Settings.Default.FilePathMax),
                Margin = new Thickness(8, 0, 8, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Focusable = true,
                TextWrapping = TextWrapping.Wrap
            };

            var iconProvider = new ScalableGreyableImageProvider(m_ShellView.LoadTangoIcon("dialog-warning"), m_ShellView.MagnificationLevel);

            var panel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            panel.Children.Add(iconProvider.IconLarge);
            panel.Children.Add(label);

            var details = new TextBoxReadOnlyCaretVisible
            {
                FocusVisualStyle = (Style)Application.Current.Resources["MyFocusVisualStyle"],

                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                TextReadOnly = filePath
            };


            var windowPopup = new PopupModalWindow(m_ShellView,
                                                   UserInterfaceStrings.EscapeMnemonic(Tobi_Plugin_Urakawa_Lang.WarningFilePathLength_Title),
                                                   panel,
                                                   PopupModalWindow.DialogButtonsSet.Ok,
                                                   PopupModalWindow.DialogButton.Ok,
                                                   true, 440, 160, details, 40, null);

            windowPopup.ShowModal();

            //if (PopupModalWindow.IsButtonEscCancel(windowPopup.ClickedDialogButton))
            //{
            //    return;
            //}

            if (File.Exists(filePath))
            {
                m_ShellView.ExecuteShellProcess(Path.GetDirectoryName(filePath));
            }

            return true;
        }
    }
}
