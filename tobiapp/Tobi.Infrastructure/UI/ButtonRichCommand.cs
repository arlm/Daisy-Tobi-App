﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Tobi.Infrastructure.Commanding;

namespace Tobi.Infrastructure.UI
{
    public class ButtonRichCommand : Button
    {
        public static readonly DependencyProperty RichCommandProperty =
            DependencyProperty.Register("RichCommand",
                                        typeof(RichDelegateCommand<object>),
                                        typeof(ButtonRichCommand),
                                        new PropertyMetadata(new PropertyChangedCallback(OnRichCommandChanged)));

        internal static void OnRichCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = d as ButtonBase;
            if (button == null)
            {
                return;
            }
            var command = e.NewValue as RichDelegateCommand<object>;
            if (command == null)
            {
                return;
            }

            ConfigureButtonFromCommand(button, command);
        }

        public static bool ShowTextLabel
        {
            get
            {
                return false;
            }
        }

        public static void ConfigureButtonFromCommand(ButtonBase button, RichDelegateCommand<object> command)
        {
            button.Command = command;

            button.ToolTip = command.LongDescription + (!String.IsNullOrEmpty(command.KeyGestureText) ? " " + command.KeyGestureText + " " : "");


            Image image = command.IconMedium;
            image.Margin = new Thickness(2, 2, 2, 2);

            if (String.IsNullOrEmpty(command.ShortDescription) || !ShowTextLabel)
            {
                button.Content = image;
            }
            else
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Children.Add(new TextBlock(new Run(command.ShortDescription)));
                panel.Children.Add(image);
                button.Content = panel;
            }
        }


        public RichDelegateCommand<object> RichCommand
        {
            get
            {
                return (RichDelegateCommand<object>)GetValue(RichCommandProperty);
            }
            set
            {
                SetValue(RichCommandProperty, value);
            }
        }
    }

    public class ToggleButtonRichCommand : ToggleButton
    {
        public static readonly DependencyProperty RichCommandProperty =
            DependencyProperty.Register("RichCommand",
                                        typeof(RichDelegateCommand<object>),
                                        typeof(ToggleButtonRichCommand),
                                        new PropertyMetadata(new PropertyChangedCallback(OnRichCommandChanged)));

        private static void OnRichCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ButtonRichCommand.OnRichCommandChanged(d, e);
        }

        public RichDelegateCommand<object> RichCommand
        {
            get
            {
                return (RichDelegateCommand<object>)GetValue(RichCommandProperty);
            }
            set
            {
                SetValue(RichCommandProperty, value);
            }
        }
    }

    public class RepeatButtonRichCommand : RepeatButton
    {
        public static readonly DependencyProperty RichCommandProperty =
            DependencyProperty.Register("RichCommand",
                                        typeof(RichDelegateCommand<object>),
                                        typeof(RepeatButtonRichCommand),
                                        new PropertyMetadata(new PropertyChangedCallback(OnRichCommandChanged)));

        private static void OnRichCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ButtonRichCommand.OnRichCommandChanged(d, e);
        }

        public RichDelegateCommand<object> RichCommand
        {
            get
            {
                return (RichDelegateCommand<object>)GetValue(RichCommandProperty);
            }
            set
            {
                SetValue(RichCommandProperty, value);
            }
        }
    }

    public interface IInputBindingManager
    {
        bool AddInputBinding(InputBinding inputBinding);
        void RemoveInputBinding(InputBinding inputBinding);
    }

    public class TwoStateButtonRichCommand : Button
    {
        public static readonly DependencyProperty InputBindingManagerProperty =
            DependencyProperty.Register("InputBindingManager",
                                        typeof(IInputBindingManager),
                                        typeof(TwoStateButtonRichCommand),
                                        new PropertyMetadata(new PropertyChangedCallback(OnInputBindingManagerChanged)));

        private static void OnInputBindingManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ;
        }

        public IInputBindingManager InputBindingManager
        {
            get
            {
                return (IInputBindingManager)GetValue(InputBindingManagerProperty);
            }
            set
            {
                SetValue(InputBindingManagerProperty, value);
            }
        }


        public static readonly DependencyProperty RichCommandOneProperty =
            DependencyProperty.Register("RichCommandOne",
                                        typeof(RichDelegateCommand<object>),
                                        typeof(TwoStateButtonRichCommand),
                                        new PropertyMetadata(new PropertyChangedCallback(OnRichCommandOneChanged)));

        private static void OnRichCommandOneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //ButtonRichCommand.OnRichCommandChanged(d, e);
        }

        public RichDelegateCommand<object> RichCommandOne
        {
            get
            {
                return (RichDelegateCommand<object>)GetValue(RichCommandOneProperty);
            }
            set
            {
                SetValue(RichCommandOneProperty, value);
            }
        }

        public static readonly DependencyProperty RichCommandTwoProperty =
            DependencyProperty.Register("RichCommandTwo",
                                        typeof(RichDelegateCommand<object>),
                                        typeof(TwoStateButtonRichCommand),
                                        new PropertyMetadata(new PropertyChangedCallback(OnRichCommandTwoChanged)));

        private static void OnRichCommandTwoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //ButtonRichCommand.OnRichCommandChanged(d, e);
        }

        public RichDelegateCommand<object> RichCommandTwo
        {
            get
            {
                return (RichDelegateCommand<object>)GetValue(RichCommandTwoProperty);
            }
            set
            {
                SetValue(RichCommandTwoProperty, value);
            }
        }

        public static readonly DependencyProperty RichCommandActiveProperty =
            DependencyProperty.Register("RichCommandActive",
                                        typeof(Boolean),
                                        typeof(TwoStateButtonRichCommand),
                                        new PropertyMetadata(true, OnRichCommandActiveChanged));

        private static void OnRichCommandActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = d as TwoStateButtonRichCommand;
            if (button == null)
            {
                return;
            }
            var choice = (Boolean)e.NewValue;

            RichDelegateCommand<object> command = button.RichCommandOne;

            if (command.KeyGesture == null && button.RichCommandTwo.KeyGesture != null)
            {
                command.KeyGestureText = button.RichCommandTwo.KeyGestureText;
            }

            if (command.KeyGesture != null
                    && command.KeyGesture.Equals(button.RichCommandTwo.KeyGesture)
                    && button.InputBindingManager != null)
            {
                button.InputBindingManager.RemoveInputBinding(button.RichCommandTwo.KeyBinding);
                button.InputBindingManager.AddInputBinding(command.KeyBinding);
            }

            if (!choice)
            {
                command = button.RichCommandTwo;

                if (command.KeyGesture == null && button.RichCommandOne.KeyGesture != null)
                {
                    command.KeyGestureText = button.RichCommandOne.KeyGestureText;
                }

                if (command.KeyGesture != null
                   && command.KeyGesture.Equals(button.RichCommandOne.KeyGesture)
                   && button.InputBindingManager != null)
                {
                    button.InputBindingManager.RemoveInputBinding(button.RichCommandOne.KeyBinding);
                    button.InputBindingManager.AddInputBinding(command.KeyBinding);
                }
            }
            ButtonRichCommand.ConfigureButtonFromCommand(button, command);
        }

        /// <summary>
        /// True => RichCommandOne (default one)
        /// False => RichCommandTwo (alternative one)
        /// </summary>
        public Boolean RichCommandActive
        {
            get
            {
                return (Boolean)GetValue(RichCommandActiveProperty);
            }
            set
            {
                SetValue(RichCommandActiveProperty, value);
            }
        }
    }
}
