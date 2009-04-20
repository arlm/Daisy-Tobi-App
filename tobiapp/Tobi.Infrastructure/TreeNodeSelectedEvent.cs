﻿using Microsoft.Practices.Composite.Presentation.Events;
using urakawa.core;

namespace Tobi.Infrastructure
{
    public class TreeNodeSelectedEvent : CompositePresentationEvent<TreeNode>
    {
    }
    public class SubTreeNodeSelectedEvent : CompositePresentationEvent<TreeNode>
    {
    }
}
