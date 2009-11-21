﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Microsoft.Practices.Composite.Logging;
using Tobi.Common;
using Tobi.Common.MVVM.Command;


namespace Tobi.Plugin.NavigationPane
{
    public partial class PagesPaneViewModel
    {
        public static RichDelegateCommand CommandFindNextPage { get; private set; }
        public static RichDelegateCommand CommandFindPrevPage { get; private set; }
        private void intializeCommands()
        {
            Logger.Log("PagesPaneViewModel.initializeCommands", Category.Debug, Priority.Medium);
            var shellView = Container.Resolve<IShellView>();
            //
            CommandFindNextPage = new RichDelegateCommand(UserInterfaceStrings.PageFindNext, UserInterfaceStrings.PageFindNext_, UserInterfaceStrings.PageFindNext_KEYS, null, () => FindNext(), () => m_Pages != null);
            CommandFindPrevPage = new RichDelegateCommand(UserInterfaceStrings.PageFindPrev, UserInterfaceStrings.PageFindPrev_, UserInterfaceStrings.PageFindPrev_KEYS, null, () => FindPrevious(), () => m_Pages != null);

           shellView.RegisterRichCommand(CommandFindNextPage);
           shellView.RegisterRichCommand(CommandFindPrevPage);
        }

    }
}
