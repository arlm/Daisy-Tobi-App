﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using urakawa.property.xml;
using urakawa.core;
using urakawa.property.channel;

namespace FlowDocumentXmlEditor.FlowDocumentExtraction.Html
{
    public class HtmlBlockExtractionVisitor : HtmlGenericExtractionVisitor
    {
        private List<Block> mExtractedBlocks = new List<Block>();
        public List<Block> ExtractedBlocks { get { return mExtractedBlocks; } }

        public HtmlBlockExtractionVisitor(Channel textCh) : base(textCh) { }

        protected override bool HandleHtmlElement(XmlProperty xmlProp, TreeNode node, Inline nodeRun)
        {
            switch (xmlProp.LocalName)
            {
                case "ul":
                    List uList = new List();
                    uList.MarkerStyle = System.Windows.TextMarkerStyle.Disc;
                    HandleListItems(uList.ListItems, node, nodeRun);
                    ExtractedBlocks.Add(uList);
                    return false;
                case "ol":
                    List oList = new List();
                    oList.MarkerStyle = System.Windows.TextMarkerStyle.Decimal;
                    HandleListItems(oList.ListItems, node, nodeRun);
                    ExtractedBlocks.Add(oList);
                    return false;
                case "p":
                    Paragraph newPara = new Paragraph();
                    HandleInlines(newPara.Inlines, node, nodeRun);
                    ExtractedBlocks.Add(newPara);
                    return false;
                default:
                    break;
            }
            return true;
        }

        public static bool ContainsHtmlBlockChild(TreeNode node)
        {
            foreach (TreeNode child in node.ListOfChildren)
            {
                if (ContainsHtmlBlock(child)) return true;
            }
            return false;
        }

        public static bool ContainsHtmlBlock(TreeNode node)
        {
            XmlProperty xmlProp = node.GetProperty<XmlProperty>();
            if (xmlProp != null)
            {
                if (xmlProp.NamespaceUri == HtmlNamespace)
                {
                    switch (xmlProp.LocalName)
                    {
                        case "p":
                        case "ul":
                        case "ol":
                            return true;
                    }
                }
            }
            return ContainsHtmlBlockChild(node);
        }

        private void HandleListItems(ListItemCollection destItemColl, TreeNode node, Inline nodeRun)
        {
            if (nodeRun != null) destItemColl.Add(new ListItem(new Paragraph(nodeRun)));
            if (node.ChildCount > 0)
            {
                HtmlListItemEntractionVisitor itemVisitor = new HtmlListItemEntractionVisitor(TextChannel);
                foreach (TreeNode child in node.ListOfChildren)
                {
                    child.AcceptDepthFirst(itemVisitor);
                }
                destItemColl.AddRange(itemVisitor.ExtractedListItems);
            }
        }

        protected override void HandleNodeRun(Inline nodeRun)
        {
            ExtractedBlocks.Add(new Paragraph(nodeRun));
        }
    }
}
