// ListWindow.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2005 Novell, Inc (http://www.novell.com)
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
//
//

using System;
using System.Collections.Generic;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Gui.Content;
using System.Linq;
using Gdk;
using Gtk;
using MonoDevelop.Components;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.Editor.Highlighting;
using MonoDevelop.Ide.Editor.Extension;

namespace MonoDevelop.Ide.CodeCompletion
{
	[Flags()]
	public enum KeyActions
	{
		None = 0,
		Process = 1,
		Ignore = 2,
		CloseWindow = 4,
		Complete = 8
	}

	public class ListWindow : PopoverWindow
	{
		const int WindowWidth = 300;

		ListWidget list;
		Widget footer;
		protected VBox vbox;
		internal MruCache cache = new MruCache();
		protected ICompletionDataList completionDataList;

		public CompletionTextEditorExtension Extension {
			get;
			set;
		}

		public CompletionCharacters CompletionCharacters {
			get {
				var ext = Extension;
				if (ext == null) // May happen in unit tests.
					return MonoDevelop.Ide.CodeCompletion.CompletionCharacters.FallbackCompletionCharacters;
				return MonoDevelop.Ide.CodeCompletion.CompletionCharacters.Get (ext.CompletionLanguage);
			}
		}
		
		public List<int> FilteredItems {
			get {
				return list.filteredItems;
			}
		}

		internal ScrolledWindow scrollbar;
		
		public ListWindow (Gtk.WindowType type) : base(type)
		{
			vbox = new VBox ();
			list = new ListWidget (this);
			list.SelectionChanged += new EventHandler (OnSelectionChanged);
			list.ScrollEvent += new ScrollEventHandler (OnScrolled);

			scrollbar = new MonoDevelop.Components.CompactScrolledWindow ();
			scrollbar.Child = list;
			list.ButtonPressEvent += delegate(object o, ButtonPressEventArgs args) {
				if (args.Event.Button == 1 && args.Event.Type == Gdk.EventType.TwoButtonPress)
					DoubleClick ();
			};
			vbox.PackEnd (scrollbar, true, true, 0);
			ContentBox.Add (vbox);
			this.AutoSelect = true;
			this.TypeHint = WindowTypeHint.Menu;
			Theme.CornerRadius = 4;
		}

		protected override void OnShown ()
		{
			var style = SyntaxModeService.GetColorStyle (IdeApp.Preferences.ColorScheme);
			Theme.SetFlatColor (style.CompletionWindow.Color);
			if (style.CompletionWindow.HasBorderColor)
				Theme.BorderColor = style.CompletionWindow.BorderColor;
			base.OnShown ();
		}

		protected virtual void DoubleClick ()
		{

		}
		
		public void ShowFooter (Widget w)
		{
			HideFooter ();
			vbox.PackStart (w, false, false, 0);
			footer = w;
		}

		public void HideFooter ()
		{
			if (footer != null) {
				vbox.Remove (footer);
				footer = null;
			}
		}

		/// <summary>
		/// This method is used to set the completion window to it's inital state.
		/// This is required for re-using the window object.
		/// </summary>
		protected internal virtual void ResetState ()
		{
			HideWhenWordDeleted = false;
			lastCommitCharEndoffset = -1;
			list.ResetState ();
		}
		
		protected int curXPos, curYPos;
		
		public void ResetSizes ()
		{
			list.CompletionString = PartialWord;
			
			var allocWidth = Allocation.Width;
			if (IsRealized && !Visible) {
				allocWidth = list.WidthRequest = WindowWidth;
				Show ();
			}

			int width = Math.Max (allocWidth, list.WidthRequest + Theme.CornerRadius * 2);
			int height = Math.Max (Allocation.Height, list.HeightRequest + 2 + (footer != null ? footer.Allocation.Height : 0) + Theme.CornerRadius * 2);
			SetSizeRequest (width, height);
			if (IsRealized) 
				Resize (width, height);
		}


		internal IListDataProvider DataProvider {
			get;
			set;
		}

		public string CurrentCompletionText {
			get {
				if (list.SelectedItem != -1 && list.AutoSelect)
					return DataProvider.GetCompletionText (list.SelectedItem);
				return null;
			}
		}

		public int SelectedItem {
			get { return list.SelectedItem; }
		}
		
		public bool AutoSelect {
			get { return list.AutoSelect; }
			set { list.AutoSelect = value; }
		}
		
		public bool SelectionEnabled {
			get { return list.SelectionEnabled; }
		}
		
		public bool AutoCompleteEmptyMatch {
			get { return list.AutoCompleteEmptyMatch; }
			set { list.AutoCompleteEmptyMatch = value; }
		}
		
		public bool AutoCompleteEmptyMatchOnCurlyBrace {
			get { return list.AutoCompleteEmptyMatchOnCurlyBrace; }
			set { list.AutoCompleteEmptyMatchOnCurlyBrace = value; }
		}
		
		public string DefaultCompletionString {
			get {
				return list.DefaultCompletionString;
			}
			set {
				list.DefaultCompletionString = value;
			}
		}
		
		public bool CloseOnSquareBrackets {
			get {
				return list.CloseOnSquareBrackets;
			}
			set {
				list.CloseOnSquareBrackets = value;
			}
		}
		
		int startOffset;
		internal int StartOffset {
			get {
				return startOffset;
			}
			set {
				startOffset = value;
				EndOffset = value + 1;
			}
		}

		public int EndOffset {
			get;
			set;
		}

		internal ICompletionWidget CompletionWidget {
			get {
				return list.CompletionWidget;
			}
			set {
				list.CompletionWidget = value;
			}
		}
		
		int lastCommitCharEndoffset = -1;
		public virtual string PartialWord {
			get {
				if (CompletionWidget == null)
					return "";
				return CompletionWidget.GetText (StartOffset, Math.Max (StartOffset, lastCommitCharEndoffset > 0 ? lastCommitCharEndoffset : CompletionWidget.CaretOffset)); 
			}
			
		}
	
		public string CurrentPartialWord {
			get {
				return !string.IsNullOrEmpty (PartialWord) ? PartialWord : DefaultCompletionString;
			}
		}

		public bool IsUniqueMatch {
			get {
				list.FilterWords ();
				return list.filteredItems.Count == 1;
			}
		}

		public ListWidget List {
			get { return list; }
		}

		/// <summary>
		/// Gets or sets a value indicating that shift was pressed during enter.
		/// </summary>
		/// <value>
		/// <c>true</c> if was shift pressed; otherwise, <c>false</c>.
		/// </value>
		public bool WasShiftPressed {
			get;
			private set;
		}

		public KeyActions PostProcessKey (KeyDescriptor descriptor)
		{
			if (CompletionWidget == null || StartOffset > CompletionWidget.CaretOffset) // CompletionWidget == null may happen in unit tests.
				return KeyActions.CloseWindow | KeyActions.Process;

			if (HideWhenWordDeleted && StartOffset >= CompletionWidget.CaretOffset)
				return KeyActions.CloseWindow | KeyActions.Process;
			switch (descriptor.SpecialKey) {
			case SpecialKey.BackSpace:
				ResetSizes ();
				UpdateWordSelection ();
				return KeyActions.Process;
			}
			var keyChar = descriptor.KeyChar;

			const string commitChars = " <>()[]{}=+-*/%~&^|!.,;:?\"'";
			if (keyChar == '[' && CloseOnSquareBrackets)
				return KeyActions.Process | KeyActions.CloseWindow;
			
			if (char.IsLetterOrDigit (keyChar) || keyChar == '_') {
				ResetSizes ();
				UpdateWordSelection ();
				return KeyActions.Process;
			}
			
			if (commitChars.Contains (keyChar)) {
				bool hasMismatches;
				var curword = PartialWord;
				int match = FindMatchedEntry (curword, out hasMismatches);
				if (match >= 0 && System.Char.IsPunctuation (keyChar)) {
					string text = DataProvider.GetCompletionText (FilteredItems [match]);
					if (!text.ToUpper ().StartsWith (curword.ToUpper (), StringComparison.Ordinal))
						match = -1;	 
				}
				if (match >= 0 && !hasMismatches && keyChar != '<' && keyChar != ' ') {
					ResetSizes ();
					UpdateWordSelection ();
					return KeyActions.Process;
				}
				if (keyChar == '.' && !list.AutoCompleteEmptyMatch && PartialWord == ".")
					list.AutoSelect = list.AutoCompleteEmptyMatch = true;
				lastCommitCharEndoffset = CompletionWidget.CaretOffset - 1;

				if (list.SelectionEnabled && CompletionCharacters.CompleteOn (keyChar)) {
					if (keyChar == '{' && !list.AutoCompleteEmptyMatchOnCurlyBrace && string.IsNullOrEmpty (list.CompletionString))
					    return KeyActions.CloseWindow | KeyActions.Process;
					return KeyActions.Complete | KeyActions.Process | KeyActions.CloseWindow;
				}
				return KeyActions.CloseWindow | KeyActions.Process;
			}

			if (char.IsPunctuation (descriptor.KeyChar) && descriptor.KeyChar != '_') {
				if (descriptor.KeyChar == ':') {
					foreach (var item in FilteredItems) {
						if (DataProvider.GetText (item).EndsWith (descriptor.KeyChar.ToString (), StringComparison.Ordinal)) {
							list.SelectedItem = item;
							return KeyActions.Complete | KeyActions.CloseWindow | KeyActions.Ignore;
						}
					}
				} else {
					var selectedItem = list.SelectedItem;
					if (selectedItem < 0 || selectedItem >= DataProvider.ItemCount)
						return KeyActions.CloseWindow;
					var text = DataProvider.GetText (selectedItem);
					if (!text.Substring (0, Math.Min (text.Length , CurrentPartialWord.Length)).EndsWith (descriptor.KeyChar.ToString (), StringComparison.Ordinal))
						return KeyActions.Process | KeyActions.CloseWindow;
				}
			}
			return KeyActions.Process;
		}
		
		public KeyActions PreProcessKey (KeyDescriptor descriptor)
		{
			switch (descriptor.SpecialKey) {
			case SpecialKey.Home:
				if ((descriptor.ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
					return KeyActions.Process;
				List.SelectionFilterIndex = 0;
				return KeyActions.Ignore;
			case SpecialKey.End:
				if ((descriptor.ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
					return KeyActions.Process;
				List.SelectionFilterIndex = List.filteredItems.Count - 1;
				return KeyActions.Ignore;
				
			case SpecialKey.Up:
				if ((descriptor.ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift) {
					if (!SelectionEnabled /*&& !CompletionWindowManager.ForceSuggestionMode*/)
						AutoCompleteEmptyMatch = AutoSelect = true;
					if (!List.InCategoryMode) {
						IdeApp.Preferences.EnableCompletionCategoryMode.Set (true);
						List.UpdateCategoryMode ();
						return KeyActions.Ignore;
					}
					List.MoveToCategory (-1);
					return KeyActions.Ignore;
				}
				if (SelectionEnabled && list.filteredItems.Count < 1)
					return KeyActions.CloseWindow | KeyActions.Process;
				if (!SelectionEnabled /*&& !CompletionWindowManager.ForceSuggestionMode*/) {
					AutoCompleteEmptyMatch = AutoSelect = true;
				} else {
					list.MoveCursor (-1);
				}
				return KeyActions.Ignore;

			case SpecialKey.Tab:
				//tab always completes current item even if selection is disabled
				if (!AutoSelect)
					AutoSelect = true;
				goto case SpecialKey.Return;

			case SpecialKey.Return:
				lastCommitCharEndoffset = CompletionWidget.CaretOffset;
				WasShiftPressed = (descriptor.ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift;
				return KeyActions.Complete | KeyActions.Ignore | KeyActions.CloseWindow;
			case SpecialKey.Down:
				if ((descriptor.ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift) {
					if (!SelectionEnabled /*&& !CompletionWindowManager.ForceSuggestionMode*/)
						AutoCompleteEmptyMatch = AutoSelect = true;
					if (!List.InCategoryMode) {
						IdeApp.Preferences.EnableCompletionCategoryMode.Set (true);
						List.UpdateCategoryMode ();
						return KeyActions.Ignore;
					}
					List.MoveToCategory (1);
					return KeyActions.Ignore;
				}
				if (SelectionEnabled && list.filteredItems.Count < 1)
					return KeyActions.CloseWindow | KeyActions.Process;
				
				if (!SelectionEnabled /*&& !CompletionWindowManager.ForceSuggestionMode*/) {
					AutoCompleteEmptyMatch = AutoSelect = true;
				} else {
					list.MoveCursor (1);
				}
				return KeyActions.Ignore;

			case SpecialKey.PageUp:
				if ((descriptor.ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
					return KeyActions.Process;
				if (list.filteredItems.Count < 2)
					return KeyActions.CloseWindow | KeyActions.Process;
				scrollbar.Vadjustment.Value = Math.Max (0, scrollbar.Vadjustment.Value - scrollbar.Vadjustment.PageSize);
				list.MoveCursor (-8);
				return KeyActions.Ignore;

			case SpecialKey.PageDown:
				if ((descriptor.ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
					return KeyActions.Process;
				if (list.filteredItems.Count < 2)
					return KeyActions.CloseWindow | KeyActions.Process;
				scrollbar.Vadjustment.Value = Math.Max (0, Math.Min (scrollbar.Vadjustment.Upper - scrollbar.Vadjustment.PageSize, scrollbar.Vadjustment.Value + scrollbar.Vadjustment.PageSize));
				list.MoveCursor (8);
				return KeyActions.Ignore;

			case SpecialKey.Left:
				//if (curPos == 0) return KeyActions.CloseWindow | KeyActions.Process;
				//curPos--;
				return KeyActions.Process;

			case SpecialKey.Right:
				//if (curPos == word.Length) return KeyActions.CloseWindow | KeyActions.Process;
				//curPos++;
				return KeyActions.Process;

//			case Gdk.Key.Caps_Lock:
//			case Gdk.Key.Num_Lock:
//			case Gdk.Key.Scroll_Lock:
//				return KeyActions.Ignore;
//
//			case Gdk.Key.Control_L:
//			case Gdk.Key.Control_R:
//			case Gdk.Key.Alt_L:
//			case Gdk.Key.Alt_R:
//			case Gdk.Key.Shift_L:
//			case Gdk.Key.Shift_R:
//			case Gdk.Key.ISO_Level3_Shift:
//				// AltGr
//				return KeyActions.Process;
			}
			if (descriptor.KeyChar == '\0')
				return KeyActions.Process;

			if (descriptor.KeyChar == ' ' && (descriptor.ModifierKeys & ModifierKeys.Shift) == ModifierKeys.Shift)
				return KeyActions.CloseWindow | KeyActions.Process;

			// special case end with punctuation like 'param:' -> don't input double punctuation, otherwise we would end up with 'param::'
			if (char.IsPunctuation (descriptor.KeyChar) && descriptor.KeyChar != '_') {
				if (descriptor.KeyChar == ':') {
					foreach (var item in FilteredItems) {
						if (DataProvider.GetText (item).EndsWith (descriptor.KeyChar.ToString (), StringComparison.Ordinal)) {
							list.SelectedItem = item;
							return KeyActions.Complete | KeyActions.CloseWindow | KeyActions.Ignore;
						}
					}
				} else {
					var selectedItem = list.SelectedItem;
					if (selectedItem < 0 || selectedItem >= DataProvider.ItemCount)
						return KeyActions.CloseWindow;
					if (DataProvider.GetText (selectedItem).EndsWith (descriptor.KeyChar.ToString (), StringComparison.Ordinal)) {
						return KeyActions.Complete | KeyActions.CloseWindow | KeyActions.Ignore;
					}
				}
			}

	/*		//don't input letters/punctuation etc when non-shift modifiers are active
			bool nonShiftModifierActive = ((Gdk.ModifierType.ControlMask | Gdk.ModifierType.MetaMask
				| Gdk.ModifierType.Mod1Mask | Gdk.ModifierType.SuperMask)
				& modifier) != 0;
			if (nonShiftModifierActive) {
				if (modifier.HasFlag (Gdk.ModifierType.ControlMask) && char.IsLetterOrDigit ((char)key))
					return KeyActions.Process | KeyActions.CloseWindow;
				return KeyActions.Ignore;
			}*/
			

			return KeyActions.Process;
		}
		
		protected bool HideWhenWordDeleted {
			get; set;
		}

		public void UpdateWordSelection ()
		{
			SelectEntry (CurrentPartialWord);
		}

		//note: finds the full match, or the best partial match
		//returns -1 if there is no match at all
		
		class WordComparer : IComparer <KeyValuePair<int, string>>
		{
			string filterWord;
			StringMatcher matcher;

			public WordComparer (string filterWord)
			{
				this.filterWord = filterWord ?? "";
				matcher = CompletionMatcher.CreateCompletionMatcher (filterWord);
			}
			
			public int Compare (KeyValuePair<int, string> xpair, KeyValuePair<int, string> ypair)
			{
				string x = xpair.Value;
				string y = ypair.Value;
				int[] xMatches = matcher.GetMatch (x) ?? new int[0];
				int[] yMatches = matcher.GetMatch (y) ?? new int[0];
				if (xMatches.Length < yMatches.Length) 
					return 1;
				if (xMatches.Length > yMatches.Length) 
					return -1;
				
				int xExact = 0;
				int yExact = 0;
				for (int i = 0; i < filterWord.Length; i++) {
					if (i < xMatches.Length && filterWord[i] == x[xMatches[i]])
						xExact++;
					if (i < yMatches.Length && filterWord[i] == y[yMatches[i]])
						yExact++;
				}
				
				if (xExact < yExact)
					return 1;
				if (xExact > yExact)
					return -1;
				
				// favor words where the match starts sooner
				if (xMatches.Length > 0 && yMatches.Length > 0 && xMatches[0] != yMatches[0])
					return xMatches[0].CompareTo (yMatches[0]);
				
				int xIndex = xpair.Key;
				int yIndex = ypair.Key;
				
				if (x.Length == y.Length)
					return xIndex.CompareTo (yIndex);
				
				return x.Length.CompareTo (y.Length);
			}
		}
		
		protected int FindMatchedEntry (string partialWord, out bool hasMismatches)
		{
			// default - word with highest match rating in the list.
			hasMismatches = true;
			int idx = -1;

			StringMatcher matcher = null;
			if (!string.IsNullOrEmpty (partialWord)) {
				matcher = CompletionMatcher.CreateCompletionMatcher (partialWord);
				string bestWord = null;
				int bestRank = int.MinValue;
				int bestIndex = 0;
				int bestIndexPriority = int.MinValue;
				for (int i = 0; i < list.filteredItems.Count; i++) {
					int index = list.filteredItems [i];
					var data = DataProvider.GetCompletionData (index);
					if (bestIndexPriority > data.PriorityGroup)
						continue;
					string text = data.DisplayText;
					int rank;
					if (!matcher.CalcMatchRank (text, out rank))
						continue;
					if (rank > bestRank || data.PriorityGroup > bestIndexPriority) {
						bestWord = text;
						bestRank = rank;
						bestIndex = i;
						bestIndexPriority = data.PriorityGroup;
					}
				}

				if (bestWord != null) {
					idx = bestIndex;
					hasMismatches = false;
					// exact match found.
					if (string.Compare (bestWord, partialWord ?? "", true) == 0)
						return idx;
				}
			}

			CompletionData currentData;
			int bestMruIndex;
			if (idx >= 0) {
				currentData = completionDataList [list.filteredItems [idx]];
				bestMruIndex = cache.GetIndex (currentData);
			} else {
				bestMruIndex = int.MaxValue;
				currentData = null;
			}
			for (int i = 0; i < list.filteredItems.Count; i++) {
				var mruData = completionDataList [list.filteredItems [i]];
				int curMruIndex = cache.GetIndex (mruData);
				if (curMruIndex == 1)
					continue;
				if (curMruIndex < bestMruIndex) {
					int r1 = 0, r2 = 0;
					if (currentData == null || matcher != null && matcher.CalcMatchRank (mruData.DisplayText, out r1) && matcher.CalcMatchRank (currentData.DisplayText, out r2)) {
						if (r1 >= r2 || PartialWord.Length <= 1) {
							bestMruIndex = curMruIndex;
							idx = i;
							currentData = mruData;
						}
					}
				}
			}
			return idx;
		}

		void SelectEntry (int n)
		{
			if (n >= 0)
				list.SelectionFilterIndex = n;
		}

		public virtual void SelectEntry (string s)
		{
			/*list.FilterWords ();
			 // disable this, because we select now the last selected entry by default (word history mode)
			//when the list is empty, disable the selection or users get annoyed by it accepting
			//the top entry automatically
			if (string.IsNullOrEmpty (s)) {
				ResetSizes ();
				list.Selection = 0;
				return;
			}*/
			bool hasMismatches;
			
			int matchedIndex = FindMatchedEntry (s, out hasMismatches);
//			ResetSizes ();
			SelectEntry (matchedIndex);
		}

		void OnScrolled (object o, ScrollEventArgs args)
		{
			if (!scrollbar.Visible)
				return;
			
			var adj = scrollbar.Vadjustment;
			var alloc = Allocation;
			
			//This widget is a special case because it's always aligned to items as it scrolls.
			//Although this means we can't use the pixel deltas for true smooth scrolling, we 
			//can still make use of the effective scrolling velocity by basing the calculation 
			//on pixels and rounding to the nearest item.
			
			double dx, dy;
			args.Event.GetPageScrollPixelDeltas (0, alloc.Height, out dx, out dy);
			if (dy == 0)
				return;
			
			var itemDelta = dy / (alloc.Height / adj.PageSize);
			double discreteItemDelta = System.Math.Round (itemDelta);
			if (discreteItemDelta == 0.0 && dy != 0.0)
				discreteItemDelta = dy > 0? 1.0 : -1.0;
			
			adj.AddValueClamped (discreteItemDelta);
			args.RetVal = true;
		}

		void OnSelectionChanged (object o, EventArgs args)
		{
			OnSelectionChanged ();
		}

		protected virtual void OnSelectionChanged ()
		{
		}
		/*
		protected override bool OnExposeEvent (Gdk.EventExpose args)
		{
			base.OnExposeEvent (args);

			int winWidth, winHeight;
			this.GetSize (out winWidth, out winHeight);
			this.GdkWindow.DrawRectangle (this.Style.ForegroundGC (StateType.Insensitive), false, 0, 0, winWidth - 1, winHeight - 1);
			return true;
		}*/
		
		public int TextOffset {
			get { return list.TextOffset + (int)Theme.CornerRadius; }
		}
	}

	interface IListDataProvider
	{
		int ItemCount { get; }
		string GetText (int n);
		string GetMarkup (int n);
		CompletionCategory GetCompletionCategory (int n);
		bool HasMarkup (int n);
		string GetCompletionText (int n);
		CompletionData GetCompletionData (int n);
		string GetDescription (int n, bool isSelected);
		string GetRightSideDescription (int n, bool isSelected);
		Xwt.Drawing.Image GetIcon (int n);
		int CompareTo (int n, int m);
	}
}

