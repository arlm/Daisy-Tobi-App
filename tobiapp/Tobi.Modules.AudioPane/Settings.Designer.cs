﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.239
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Tobi.Plugin.AudioPane {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "10.0.0.0")]
    public sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool Audio_EnableSkippableText {
            get {
                return ((bool)(this["Audio_EnableSkippableText"]));
            }
            set {
                this["Audio_EnableSkippableText"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AudioWaveForm_DisableDraw {
            get {
                return ((bool)(this["AudioWaveForm_DisableDraw"]));
            }
            set {
                this["AudioWaveForm_DisableDraw"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AudioWaveForm_UseVectorAtResize {
            get {
                return ((bool)(this["AudioWaveForm_UseVectorAtResize"]));
            }
            set {
                this["AudioWaveForm_UseVectorAtResize"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("400")]
        public double AudioWaveForm_TimeStep {
            get {
                return ((double)(this["AudioWaveForm_TimeStep"]));
            }
            set {
                this["AudioWaveForm_TimeStep"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("RenderTargetBitmap")]
        public global::Tobi.Plugin.AudioPane.WaveFormRenderMethod AudioWaveForm_RenderMethod {
            get {
                return ((global::Tobi.Plugin.AudioPane.WaveFormRenderMethod)(this["AudioWaveForm_RenderMethod"]));
            }
            set {
                this["AudioWaveForm_RenderMethod"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("20")]
        public double AudioWaveForm_TextPreRenderThreshold {
            get {
                return ((double)(this["AudioWaveForm_TextPreRenderThreshold"]));
            }
            set {
                this["AudioWaveForm_TextPreRenderThreshold"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool UseFriendlyTimeFormat {
            get {
                return ((bool)(this["UseFriendlyTimeFormat"]));
            }
            set {
                this["UseFriendlyTimeFormat"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool Audio_ButtonBarVisible {
            get {
                return ((bool)(this["Audio_ButtonBarVisible"]));
            }
            set {
                this["Audio_ButtonBarVisible"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("3")]
        public double AudioWaveForm_Resolution {
            get {
                return ((double)(this["AudioWaveForm_Resolution"]));
            }
            set {
                this["AudioWaveForm_Resolution"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AudioWaveForm_IsFilled {
            get {
                return ((bool)(this["AudioWaveForm_IsFilled"]));
            }
            set {
                this["AudioWaveForm_IsFilled"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AudioWaveForm_IsBordered {
            get {
                return ((bool)(this["AudioWaveForm_IsBordered"]));
            }
            set {
                this["AudioWaveForm_IsBordered"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AudioWaveForm_IsStroked {
            get {
                return ((bool)(this["AudioWaveForm_IsStroked"]));
            }
            set {
                this["AudioWaveForm_IsStroked"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("default")]
        public string Audio_OutputDevice {
            get {
                return ((string)(this["Audio_OutputDevice"]));
            }
            set {
                this["Audio_OutputDevice"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("default")]
        public string Audio_InputDevice {
            get {
                return ((string)(this["Audio_InputDevice"]));
            }
            set {
                this["Audio_InputDevice"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("default")]
        public string Audio_TTS_Voice {
            get {
                return ((string)(this["Audio_TTS_Voice"]));
            }
            set {
                this["Audio_TTS_Voice"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("pagenum,sidebar,note,footnote,endnote,rearnote,prodnote,annotation,")]
        public string Skippables {
            get {
                return ((string)(this["Skippables"]));
            }
            set {
                this["Skippables"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FF7FFF00")]
        public global::System.Windows.Media.Color AudioWaveForm_Color_Border {
            get {
                return ((global::System.Windows.Media.Color)(this["AudioWaveForm_Color_Border"]));
            }
            set {
                this["AudioWaveForm_Color_Border"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FF32CD32")]
        public global::System.Windows.Media.Color AudioWaveForm_Color_Fill {
            get {
                return ((global::System.Windows.Media.Color)(this["AudioWaveForm_Color_Fill"]));
            }
            set {
                this["AudioWaveForm_Color_Fill"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FF000000")]
        public global::System.Windows.Media.Color AudioWaveForm_Color_Back {
            get {
                return ((global::System.Windows.Media.Color)(this["AudioWaveForm_Color_Back"]));
            }
            set {
                this["AudioWaveForm_Color_Back"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FF00FF00")]
        public global::System.Windows.Media.Color AudioWaveForm_Color_Stroke {
            get {
                return ((global::System.Windows.Media.Color)(this["AudioWaveForm_Color_Stroke"]));
            }
            set {
                this["AudioWaveForm_Color_Stroke"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FFFF0000")]
        public global::System.Windows.Media.Color AudioWaveForm_Color_CursorBorder {
            get {
                return ((global::System.Windows.Media.Color)(this["AudioWaveForm_Color_CursorBorder"]));
            }
            set {
                this["AudioWaveForm_Color_CursorBorder"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FFFFD700")]
        public global::System.Windows.Media.Color AudioWaveForm_Color_CursorFill {
            get {
                return ((global::System.Windows.Media.Color)(this["AudioWaveForm_Color_CursorFill"]));
            }
            set {
                this["AudioWaveForm_Color_CursorFill"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FF00BFFF")]
        public global::System.Windows.Media.Color AudioWaveForm_Color_Selection {
            get {
                return ((global::System.Windows.Media.Color)(this["AudioWaveForm_Color_Selection"]));
            }
            set {
                this["AudioWaveForm_Color_Selection"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FF4682B4")]
        public global::System.Windows.Media.Color AudioWaveForm_Color_Phrases {
            get {
                return ((global::System.Windows.Media.Color)(this["AudioWaveForm_Color_Phrases"]));
            }
            set {
                this["AudioWaveForm_Color_Phrases"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("#FFFFFFFF")]
        public global::System.Windows.Media.Color AudioWaveForm_Color_TimeText {
            get {
                return ((global::System.Windows.Media.Color)(this["AudioWaveForm_Color_TimeText"]));
            }
            set {
                this["AudioWaveForm_Color_TimeText"] = value;
            }
        }
    }
}
