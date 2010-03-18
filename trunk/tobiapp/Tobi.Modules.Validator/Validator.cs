﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.Composite.Logging;
using Tobi.Common;
using Tobi.Common.Validation;
using urakawa;
using urakawa.events.undo;

namespace Tobi.Plugin.Validator
{
    [Export(typeof(Validator)), PartCreationPolicy(CreationPolicy.Shared)]
    public class Validator : AbstractValidator, IPartImportsSatisfiedNotification
    {
#pragma warning disable 1591 // non-documented method
        public void OnImportsSatisfied()
#pragma warning restore 1591
        {
            //#if DEBUG
            //            Debugger.Break();
            //#endif
        
        }

        private readonly IEventAggregator m_EventAggregator;
        private readonly ILoggerFacade m_Logger;

        private readonly IUrakawaSession m_UrakawaSession;
        public readonly IEnumerable<IValidator> m_Validators;
        private bool m_RanOnce;

        [ImportingConstructor]
        public Validator(
            ILoggerFacade logger,
            IEventAggregator eventAggregator,
            [ImportMany(typeof(IValidator), RequiredCreationPolicy = CreationPolicy.Shared, AllowRecomposition = false)]
            IEnumerable<IValidator> validators,
            [Import(typeof(IUrakawaSession), RequiredCreationPolicy = CreationPolicy.Shared, AllowDefault = false)]
            IUrakawaSession session)
        {
            m_EventAggregator = eventAggregator;
            m_Logger = logger;
            m_RanOnce = false;
            m_Validators = validators;
            m_UrakawaSession = session;
            
            IsValid = true;

            if (m_UrakawaSession.DocumentProject != null)
            {
                OnProjectLoaded(m_UrakawaSession.DocumentProject);
            }

            m_EventAggregator.GetEvent<ProjectLoadedEvent>().Subscribe(OnProjectLoaded, ProjectLoadedEvent.THREAD_OPTION);
            m_EventAggregator.GetEvent<ProjectUnLoadedEvent>().Subscribe(OnProjectUnLoaded, ProjectUnLoadedEvent.THREAD_OPTION);
        }

        //EventAggregator.GetEvent<TypeConstructedEvent>().Publish(GetType());
        //SubscriptionToken token = EventAggregator.GetEvent<TypeConstructedEvent>().Subscribe(OnTypeConstructed_IUrakawaSession, ThreadOption.UIThread, false, type => typeof(IUrakawaSession).IsAssignableFrom(type));

        private void OnProjectLoaded(Project project)
        {
            project.Presentations.Get(0).UndoRedoManager.CommandDone += OnUndoRedoManagerChanged;
            project.Presentations.Get(0).UndoRedoManager.CommandReDone += OnUndoRedoManagerChanged;
            project.Presentations.Get(0).UndoRedoManager.CommandUnDone += OnUndoRedoManagerChanged;
            project.Presentations.Get(0).UndoRedoManager.TransactionCancelled += OnUndoRedoManagerChanged;
            m_RanOnce = false;
            Validate();
        }

        private void OnProjectUnLoaded(Project project)
        {
            IsValid = true;

            project.Presentations.Get(0).UndoRedoManager.CommandDone -= OnUndoRedoManagerChanged;
            project.Presentations.Get(0).UndoRedoManager.CommandReDone -= OnUndoRedoManagerChanged;
            project.Presentations.Get(0).UndoRedoManager.CommandUnDone -= OnUndoRedoManagerChanged;
            project.Presentations.Get(0).UndoRedoManager.TransactionCancelled -= OnUndoRedoManagerChanged;
        }

        private void OnUndoRedoManagerChanged(object sender, UndoRedoManagerEventArgs e)
        {
            Validate();
        }

        public override string Name
        {
            get { return Tobi_Plugin_Validator_Lang.Validator_Name; }    // TODO LOCALIZE Validator_Name
        }

        public override string Description
        {
            get { return Tobi_Plugin_Validator_Lang.Validator_Description; }    // TODO LOCALIZE Validator_Description
        }

        public override IEnumerable<ValidationItem> ValidationItems
        {
            get
            {
                if (IsValid)
                {
                    yield return null;
                }

                foreach (IValidator validator in m_Validators)
                {
                    if (validator.IsValid)
                    {
                        continue;
                    }

                    foreach (ValidationItem item in validator.ValidationItems)
                    {
                        yield return item;
                    }
                }

                yield break;
            }
        }

        public override bool Validate()
        {
            // Is LINQ really more readble for the average developer ?
            //bool m_IsValid = Validators.Aggregate(true, (current, validator) => current && validator.Validate());

            bool isValid = true;
            
            foreach (IValidator validator in m_Validators)
            {
                bool result = true;
                if (!validator.ShouldRunOnlyOnce || 
                    (validator.ShouldRunOnlyOnce && !m_RanOnce))
                {
                    result = validator.Validate();
                    isValid = isValid && result;
                }
                
            }

            IsValid = isValid;

            m_RanOnce = true;

            

            return IsValid;
        }
    }
}