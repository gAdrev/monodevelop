//
// CompletionAppearancePanel.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2013 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide.Editor.Extension;
using MonoDevelop.Ide;

namespace MonoDevelop.SourceEditor.OptionPanels
{
	[System.ComponentModel.ToolboxItem(false)]
	partial class CompletionAppearancePanel : Gtk.Bin, IOptionsPanel
	{
		public CompletionAppearancePanel ()
		{
			this.Build ();
			filterByBrowsableCheckbutton.Toggled += FilterToggled;
		}

		void FilterToggled (object sender, EventArgs e)
		{
			label4.Sensitive = label5.Sensitive =
			normalOnlyRadiobutton.Sensitive = includeAdvancedRadiobutton.Sensitive = filterByBrowsableCheckbutton.Active;
		}

		#region IOptionsPanel implementation

		void IOptionsPanel.Initialize (OptionsDialog dialog, object dataObject)
		{
		}

		Gtk.Widget IOptionsPanel.CreatePanelWidget ()
		{
			filterByBrowsableCheckbutton.Active = IdeApp.Preferences.FilterCompletionListByEditorBrowsable;
			normalOnlyRadiobutton.Active = !IdeApp.Preferences.IncludeEditorBrowsableAdvancedMembers;
			includeAdvancedRadiobutton.Active = IdeApp.Preferences.IncludeEditorBrowsableAdvancedMembers;
			FilterToggled (this, EventArgs.Empty);
			return this;
		}

		bool IOptionsPanel.IsVisible ()
		{
			return true;
		}

		bool IOptionsPanel.ValidateChanges ()
		{
			return true;
		}

		void IOptionsPanel.ApplyChanges ()
		{
			IdeApp.Preferences.FilterCompletionListByEditorBrowsable.Value = filterByBrowsableCheckbutton.Active;
			IdeApp.Preferences.IncludeEditorBrowsableAdvancedMembers.Value = includeAdvancedRadiobutton.Active;
		}

		#endregion
	}
}

