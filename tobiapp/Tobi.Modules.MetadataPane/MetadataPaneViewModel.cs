﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.Composite.Logging;
using Microsoft.Practices.Composite.Presentation.Events;
using Microsoft.Practices.Unity;
using Tobi.Infrastructure;
using Tobi.Infrastructure.Commanding;
using Tobi.Infrastructure.UI;
using urakawa;
using urakawa.metadata;
using urakawa.events.presentation;
using urakawa.events.metadata;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;

namespace Tobi.Modules.MetadataPane
{
    /// <summary>
    /// ViewModel for the MetadataPane
    /// </summary>
    public class MetadataPaneViewModel : ViewModelBase
    {
        #region Construction

        protected IUnityContainer Container { get; private set; }
        protected IEventAggregator EventAggregator { get; private set; }
        public ILoggerFacade Logger { get; private set; }
        
        ///<summary>
        /// Dependency-Injected constructor
        ///</summary>
        public MetadataPaneViewModel(IUnityContainer container, IEventAggregator eventAggregator, ILoggerFacade logger)
        {
            Container = container;
            EventAggregator = eventAggregator;
            Logger = logger;
            m_Metadatas = null;
            Initialize();
        }

        #endregion Construction

        #region Initialization

        protected IMetadataPaneView View { get; private set; }
        public void SetView(IMetadataPaneView view)
        {
            View = view;
            
        }

        protected void Initialize()
        {
            Logger.Log("MetadataPaneViewModel.Initialize", Category.Debug, Priority.Medium);

            m_Project = null;

            initializeCommands();

            EventAggregator.GetEvent<ProjectLoadedEvent>().Subscribe(OnProjectLoaded, ThreadOption.UIThread);
            EventAggregator.GetEvent<ProjectUnLoadedEvent>().Subscribe(OnProjectUnLoaded, ThreadOption.UIThread);

            StatusText = "everything is wonderful";

            RefreshDataTemplateSelectors();
       }

        private Project m_Project;
        public Project Project
        {
            get { return m_Project; }
            set
            {
                if (m_Project == value) return;
                m_Project = value;
                OnPropertyChanged(() => Project);
            }
        }

        private void OnProjectUnLoaded(Project obj)
        {
            OnProjectLoaded(null);
        }

        private void OnProjectLoaded(Project project)
        {
            Logger.Log("MetadataPaneViewModel.OnProjectLoaded" + (project == null ? "(null)" : ""), 
                Category.Debug, Priority.Medium);
            Project = project;
            ContentTemplateSelectorProperty = new ContentTemplateSelector((MetadataPaneView)View);
            NameTemplateSelectorProperty = new NameTemplateSelector((MetadataPaneView)View);
        }

        
        #endregion Initialization

        #region Commands

        public RichDelegateCommand<object> CommandShowMetadataPane { get; private set; }
        
        private void initializeCommands()
        {
            Logger.Log("MetadataPaneViewModel.initializeCommands", Category.Debug, Priority.Medium);

            var shellPresenter = Container.Resolve<IShellPresenter>();
            
            CommandShowMetadataPane = new RichDelegateCommand<object>(UserInterfaceStrings.ShowMetadata,
                UserInterfaceStrings.ShowMetadata_,
                UserInterfaceStrings.ShowMetadata_KEYS,
                (VisualBrush)Application.Current.FindResource("accessories-text-editor"),
                obj => showMetadata(), obj => canShowMetadata);

            shellPresenter.RegisterRichCommand(CommandShowMetadataPane);
        }

        private void showMetadata()
        {
            Logger.Log("MetadataPaneViewModel.showMetadata", Category.Debug, Priority.Medium);

            var shellPresenter = Container.Resolve<IShellPresenter>();
            var window = shellPresenter.View as Window;

            var windowPopup = new PopupModalWindow(window ?? Application.Current.MainWindow,
                                                   UserInterfaceStrings.EscapeMnemonic(
                                                       UserInterfaceStrings.ShowMetadata_),
                                                   View,
                                                   PopupModalWindow.DialogButtonsSet.OkCancel,
                                                   PopupModalWindow.DialogButton.Ok,
                                                   true, 500, 500);
            windowPopup.Show();
        }

        [NotifyDependsOn("Project")]
        private bool canShowMetadata
        {
            get { return Project != null && Project.NumberOfPresentations > 0; }
        }

        #endregion Commands

        public void RefreshDataTemplateSelectors()
        {
            ContentTemplateSelectorProperty = new ContentTemplateSelector((MetadataPaneView)View);
            NameTemplateSelectorProperty = new NameTemplateSelector((MetadataPaneView)View);
        }
        private string m_StatusText;
        public string StatusText
        {
            get
            {
                return m_StatusText;
            }
            set
            {
                m_StatusText = value;
                OnPropertyChanged(() => StatusText);
            }
        }

        private ObservableMetadataCollection m_Metadatas;
        
        [NotifyDependsOn("Project")]
        public ObservableMetadataCollection Metadatas 
        {
            get
            {
                if (Project == null || Project.NumberOfPresentations <= 0)
                {
                    m_Metadatas = null;
                }
                else
                {
                    if (m_Metadatas == null)
                    {
                        m_Metadatas = new ObservableMetadataCollection(Project.GetPresentation(0).ListOfMetadata);
                        Project.GetPresentation(0).MetadataAdded += new System.EventHandler<MetadataAddedEventArgs>
                            (m_Metadatas.OnMetadataAdded);
                        Project.GetPresentation(0).MetadataDeleted += new System.EventHandler<MetadataDeletedEventArgs>
                            (m_Metadatas.OnMetadataDeleted);
                    }
                }
                return m_Metadatas;
            }
        }
        public void CreateFakeData()
        {
            List<Metadata> list = Project.GetPresentation(0).ListOfMetadata;
            Metadata metadata = list.Find(s => s.Name == "dc:Title");
            if (metadata != null)
            {
                //metadata.Content = "Fake book about fake things";
                metadata.Name = "dtb:sourceDate";
            }
        }

        public void RemoveMetadata(NotifyingMetadataItem metadata)
        {
            //TODO: warn against or prevent removing required metadata
            Project.GetPresentation(0).DeleteMetadata(metadata.UrakawaMetadata);
        }

        public void AddEmptyMetadata()
        {
            Metadata metadata = new Metadata();
            metadata.Name = "";
            metadata.Content = "";
            Project.GetPresentation(0).AddMetadata(metadata);
        }

        /// <summary>
        /// based on the existing metadata, return a list of metadata fields available
        /// for addition
        /// </summary>
        public List<string> AvailableMetadata
        {
            get
            {
                return GetAvailableMetadata();
            }
        }
        private List<string> GetAvailableMetadata()
        {
            List<Metadata> metadatas = Project.GetPresentation(0).ListOfMetadata;
            List<string> list = new List<string>();
            foreach (SupportedMetadataItem metadataItem in SupportedMetadataList.MetadataList)
            {
                //is this already in our project metadata list, and can it be repeated?
                Metadata alreadyExists = metadatas.Find(s => s.Name == metadataItem.Name);
                if (alreadyExists == null)
                    list.Add(metadataItem.Name);
                else if (alreadyExists != null && metadataItem.IsRepeatable)
                    list.Add(metadataItem.Name);
            }
            return list;    
        }

        public void ValidateMetadata()
        {
            List<string> missing = new List<string>();
            List<Metadata> metadatas = Project.GetPresentation(0).ListOfMetadata;
             foreach (SupportedMetadataItem metadataItem in SupportedMetadataList.MetadataList)
             {
                 Metadata exists = metadatas.Find(s => s.Name == metadataItem.Name);
                 if (metadataItem.Occurence == MetadataOccurence.Required && 
                     metadataItem.IsReadOnly == false &&
                     exists == null)
                 {
                     missing.Add(metadataItem.Name);
                 }
             }

            if (missing.Count > 0)
            {
                string temp = string.Join(", ", missing.ToArray());
                StatusText = string.Format("Missing: {0}", temp);
            }
            else
            {
                StatusText = "your metadata is great";
            }
                
        }

        private NameTemplateSelector m_NameTemplateSelector = null;
        public NameTemplateSelector NameTemplateSelectorProperty
        {
            get
            {
                return m_NameTemplateSelector;
            }
            private set
            {
                m_NameTemplateSelector = value;
                OnPropertyChanged(() => NameTemplateSelectorProperty);
            }
        }

        private ContentTemplateSelector m_ContentTemplateSelector = null;
        public ContentTemplateSelector ContentTemplateSelectorProperty
        {
            get
            {
                return m_ContentTemplateSelector;
            }
            private set
            {
                m_ContentTemplateSelector = value;
                OnPropertyChanged(() => ContentTemplateSelectorProperty);
            }
        }
    }
    
    public class ContentTemplateSelector : DataTemplateSelector
    {
        public DataTemplate OptionalStringTemplate { get; set; }
        public DataTemplate OptionalDateTemplate { get; set; }
        public DataTemplate RequiredStringTemplate { get; set; }
        public DataTemplate RequiredDateTemplate { get; set; }
        public DataTemplate ReadOnlyTemplate { get; set; }
        public DataTemplate DefaultTemplate { get; set; }

        private MetadataPaneView m_View;
        public ContentTemplateSelector(MetadataPaneView view)
        {
            if (view == null) return;
            m_View = view;
            
            OptionalStringTemplate = (DataTemplate)m_View.list.Resources["OptionalStringContent"];
            OptionalDateTemplate = (DataTemplate)m_View.list.Resources["OptionalDateContent"];
            RequiredStringTemplate = (DataTemplate)m_View.list.Resources["RequiredStringContent"];
            RequiredDateTemplate = (DataTemplate)m_View.list.Resources["RequiredDateContent"];
            ReadOnlyTemplate = (DataTemplate)m_View.list.Resources["ReadOnlyContent"];
            DefaultTemplate = OptionalStringTemplate;
        }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            NotifyingMetadataItem metadata = (NotifyingMetadataItem)item;

            List<Tobi.Modules.MetadataPane.SupportedMetadataItem> list =
                Tobi.Modules.MetadataPane.SupportedMetadataList.MetadataList;
            int index = list.FindIndex(0, s => s.Name == metadata.Name);
            if (index != -1)
            {
                Tobi.Modules.MetadataPane.SupportedMetadataItem metaitem = list[index];
                //TODO: this assumes that when a field is readonly, we will just display it as a default (short) string
                //this is probably an ok assumption for now, but we'll want to change it later.
                if (metaitem.IsReadOnly)
                    return ReadOnlyTemplate;

                if (metaitem.FieldType == SupportedMetadataFieldType.Date)
                {
                    if (metaitem.Occurence == MetadataOccurence.Required)
                        return RequiredDateTemplate;
                    else
                        return OptionalDateTemplate;
                }

                else if (metaitem.FieldType == SupportedMetadataFieldType.ShortString ||
                    metaitem.FieldType == SupportedMetadataFieldType.LongString)
                {
                    if (metaitem.Occurence == MetadataOccurence.Required)
                        return RequiredStringTemplate;
                    else
                        return OptionalStringTemplate;
                }

                else
                {
                    return DefaultTemplate;
                }
            }
            else
            {
                return DefaultTemplate;
            }
        }
    }

    public class NameTemplateSelector : DataTemplateSelector
    {
        public DataTemplate OptionalTemplate { get; set; }
        public DataTemplate RecommendedTemplate { get; set; }
        public DataTemplate RequiredTemplate { get; set; }

        private MetadataPaneView m_View;
        public NameTemplateSelector(MetadataPaneView view)
        {
            if (view == null) return;

            m_View = view;
            OptionalTemplate = (DataTemplate)m_View.list.Resources["OptionalName"];
            RecommendedTemplate = (DataTemplate)m_View.list.Resources["RecommendedName"];
            RequiredTemplate = (DataTemplate)m_View.list.Resources["RequiredName"];
        }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {

            NotifyingMetadataItem metadata = (NotifyingMetadataItem)item;

            List<Tobi.Modules.MetadataPane.SupportedMetadataItem> list =
                    Tobi.Modules.MetadataPane.SupportedMetadataList.MetadataList;

            int index = list.FindIndex(0, s => s.Name == metadata.Name);

            if (index != -1)
            {
                Tobi.Modules.MetadataPane.SupportedMetadataItem metaitem = list[index];

                if (metaitem.Occurence == MetadataOccurence.Required)
                    return RequiredTemplate;
                else if (metaitem.Occurence == MetadataOccurence.Recommended)
                    return RecommendedTemplate;
            }
            return OptionalTemplate;
        }
    }
    
    public class NotifyingMetadataItem : PropertyChangedNotifyBase
    {
        private Metadata m_Metadata;
        public Metadata UrakawaMetadata
        {
            get
            {
                return m_Metadata;
            }
        }
        public NotifyingMetadataItem(Metadata metadata)
        {
            m_Metadata = metadata;
            m_Metadata.NameChanged += new System.EventHandler<NameChangedEventArgs>(this.OnNameChanged);
            m_Metadata.ContentChanged += new System.EventHandler<ContentChangedEventArgs>(this.OnContentChanged);
        }
        ~NotifyingMetadataItem()
        {
            RemoveEvents();
        }

        public string Content
        {
            get
            {
                return m_Metadata.Content;
            }
            set
            {
                if (m_Metadata.Content == value) return;
                
                //we have to protect the Urakawa SDK from null metadata values ... 
                if (value != null)
                {
                    m_Metadata.Content = value;
                    OnPropertyChanged(() => Content);
                }
                
            }
        }

        public string Name
        {
            get
            {
                return m_Metadata.Name;
            }
            set
            {
                if (m_Metadata.Name == value) return;
                //we have to protect the Urakawa SDK from null metadata values ... 
                if (value != null)
                {
                    m_Metadata.Name = value;
                    OnPropertyChanged(() => Name);
                }
            }
        }

        void OnContentChanged(object sender, ContentChangedEventArgs e)
        {
           OnPropertyChanged(() => Content);
        }

        void OnNameChanged(object sender, NameChangedEventArgs e)
        {
            OnPropertyChanged(() => Name);
        }

        internal void RemoveEvents()
        {
            m_Metadata.NameChanged -= new System.EventHandler<NameChangedEventArgs>(OnNameChanged);
            m_Metadata.ContentChanged -= new System.EventHandler<ContentChangedEventArgs>(OnContentChanged);
        }

        

    }

    
    public class ObservableMetadataCollection : ObservableCollection<NotifyingMetadataItem>
    {
        public ObservableMetadataCollection(List<Metadata> metadatas)
        {
            foreach (Metadata metadata in metadatas)
            {
                this.Add(new NotifyingMetadataItem(metadata));
            }
        }
        #region sdk-events
        public void OnMetadataDeleted(object sender, MetadataDeletedEventArgs eventArgs)
        {
            foreach (NotifyingMetadataItem metadata in this)
            {
                if (metadata.Content == eventArgs.DeletedMetadata.Content &&
                    metadata.Name == eventArgs.DeletedMetadata.Name)
                {
                    this.Remove(metadata);
                    metadata.RemoveEvents();                
                    break;
                }
            }
        }

        public void OnMetadataAdded(object sender, MetadataAddedEventArgs eventArgs)
        {
            this.Add(new NotifyingMetadataItem(eventArgs.AddedMetadata));
        }
        #endregion sdk-events

    }

    
}
