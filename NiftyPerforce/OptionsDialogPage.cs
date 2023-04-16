// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.

/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace NiftyPerforce
{
    /// <summary>
    /// Summary description for SccProviderOptions.
    /// </summary>
    public class OptionsDialogPage : DialogPage
    {
        [Category("Operation")]
        [Description("Controls if we automagically check out files from perforce upon keypress (loose some performance in editor)")]
        public bool AutoCheckoutOnEdit { get; set; } = false;

        [Category("Operation")]
        [Description("Automatically check out projects on edit properties (loose some performance in editor)")]
        public bool AutoCheckoutProject { get; set; } = false;

        [Category("Operation")]
        [Description("Controls if we automagically check out files from perforce before saving")]
        public bool AutoCheckoutOnSave { get; set; } = true;

        [Category("Operation")]
        [Description("Automagically add files to perforce")]
        public bool AutoAdd { get; set; } = false;

        [Category("Operation")]
        [Description("Automagically delete files from perforce when we're deleting files from visual studio (fairly dangerous)")]
        public bool AutoDelete { get; set; } = false;

        [Category("Operation")]
        [Description("Try to do a p4 edit even though the file is writable. Useful if you have a git repository above your p4 workspace. Costly!")]
        public bool IgnoreReadOnlyOnEdit { get; set; } = false;

        [Category("Connection")]
        [Description("Use config from system. Effectivly disables the settings inside this dialog for the client etc and picks up the settings from the registry/p4config environment.")]
        public bool UseSystemEnv { get; set; } = true;

        [Category("Connection")]
        [Description("Perforce port number")]
        public string Port { get; set; } = string.Empty;

        [Category("Connection")]
        [Description("Perforce client")]
        public string Client { get; set; } = string.Empty;

        [Category("Connection")]
        [Description("Perforce username")]
        public string Username { get; set; } = string.Empty;

        [Category("Branching")]
        [Description("Where we can find the mainline version of this file")]
        public string MainLinePath { get; set; } = string.Empty;

#if NIFTY_LEGACY
        [Category("VSIX Legacy")]
        [Description("Clean the legacy nifty perforce commands from the IDE when clicking ok. Will not be persisted.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool CleanLegacyNiftyCommands { get; set; } = false;
#endif

        public event EventHandler? OnApplyEvent;

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
            if (e?.ApplyBehavior == ApplyKind.Apply)
                OnApplyEvent?.Invoke(this, EventArgs.Empty);
        }

        protected override void SaveSetting(PropertyDescriptor property)
        {
            base.SaveSetting(property);
        }
    }
}
