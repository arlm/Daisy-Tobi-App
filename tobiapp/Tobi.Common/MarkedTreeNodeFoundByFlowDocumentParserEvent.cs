﻿using Microsoft.Practices.Composite.Presentation.Events;
using urakawa.core;

namespace Tobi.Common
{
    public class MarkedTreeNodeFoundByFlowDocumentParserEvent : CompositePresentationEvent<TreeNode>
    {
        public static ThreadOption THREAD_OPTION = ThreadOption.PublisherThread;
    }
}
