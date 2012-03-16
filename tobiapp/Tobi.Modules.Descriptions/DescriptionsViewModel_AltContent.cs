﻿using System;
using System.Collections.Generic;
using Tobi.Common.MVVM;
using urakawa.commands;
using urakawa.core;
using urakawa.daisy;
using urakawa.property.alt;
using urakawa.xuk;

namespace Tobi.Plugin.Descriptions
{
    public partial class DescriptionsViewModel
    {
        public void AddDescription(string uid, string descriptionName)
        {
            Tuple<TreeNode, TreeNode> selection = m_UrakawaSession.GetTreeNodeSelection();
            TreeNode node = selection.Item2 ?? selection.Item1;
            if (node == null) return;

            //var altProp = node.GetOrCreateAlternateContentProperty();
            //if (altProp == null) return;

            AlternateContent altContent = node.Presentation.AlternateContentFactory.CreateAlternateContent();

            AlternateContentAddCommand cmd1 =
                node.Presentation.CommandFactory.CreateAlternateContentAddCommand(node, altContent);
            node.Presentation.UndoRedoManager.Execute(cmd1);

            RaisePropertyChanged(() => Descriptions);

            if (!string.IsNullOrEmpty(uid))
            {
                AddMetadata(null, altContent, XmlReaderWriterHelper.XmlId, uid);
            }
            if (!string.IsNullOrEmpty(descriptionName))
            {
                AddMetadata(null, altContent, DiagramContentModelHelper.DiagramElementName, descriptionName);
            }
        }

        public void RemoveDescription(AlternateContent altContent)
        {
            Tuple<TreeNode, TreeNode> selection = m_UrakawaSession.GetTreeNodeSelection();
            TreeNode node = selection.Item2 ?? selection.Item1;
            if (node == null) return;

            var altProp = node.GetProperty<AlternateContentProperty>();
            if (altProp == null) return;

            if (altProp.AlternateContents.IndexOf(altContent) < 0) return;

            AlternateContentRemoveCommand cmd1 =
                node.Presentation.CommandFactory.CreateAlternateContentRemoveCommand(node, altContent);
            node.Presentation.UndoRedoManager.Execute(cmd1);

            RaisePropertyChanged(() => Descriptions);
        }


        private AlternateContent m_SelectedAlternateContent;
        public void SetSelectedAlternateContent(AlternateContent altContent)
        {
            m_SelectedAlternateContent = altContent;
            RaisePropertyChanged(() => Descriptions);
        }

        [NotifyDependsOn("Descriptions")]
        public bool HasDescriptions
        {
            get
            {
                if (m_UrakawaSession.DocumentProject == null) return false;

                Tuple<TreeNode, TreeNode> selection = m_UrakawaSession.GetTreeNodeSelection();
                TreeNode node = selection.Item2 ?? selection.Item1;
                if (node == null) return false;

                AlternateContentProperty altProp = node.GetProperty<AlternateContentProperty>();
                if (altProp == null) return false;

                //return new ObservableCollection<Metadata>(altProp.Metadatas.ContentsAs_Enumerable);
                return altProp.AlternateContents.Count > 0;
            }
        }

        public IEnumerable<AlternateContent> Descriptions //ObservableCollection
        {
            get
            {
                if (m_UrakawaSession.DocumentProject == null) return null;

                Tuple<TreeNode, TreeNode> selection = m_UrakawaSession.GetTreeNodeSelection();
                TreeNode node = selection.Item2 ?? selection.Item1;
                if (node == null) return null;

                AlternateContentProperty altProp = node.GetProperty<AlternateContentProperty>();
                if (altProp == null) return null;

                //return new ObservableCollection<Metadata>(altProp.Metadatas.ContentsAs_Enumerable);
                return altProp.AlternateContents.ContentsAs_Enumerable;
            }
        }

    }
}
