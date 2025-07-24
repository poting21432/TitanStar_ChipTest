using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using PropertyChanged;
using rm.Trie;
using Support.Wpf.Models;

namespace Support.Wpf.Compact
{
    [AddINotifyPropertyChangedInterface]
    public class ModelTrie
    {
        public Trie Trie { get; set; } = new();
        public ObservableCollection<string> Suggestions { get; set; } = new ObservableCollection<string>();
        public Visibility VisiblilitySuggest { get; set; } = Visibility.Collapsed;

        private bool Selected { get; set; } = false;

        private string textBuffer = string.Empty;
        private string selectedText = string.Empty;
        public string SelectedText 
        {
            get => selectedText;
            set
            {
                textBuffer = value;
                if (!string.IsNullOrEmpty(value) || textBuffer == selectedText)
                {
                    Selected = true;
                    selectedText = value;
                    SearchText = value;
                    Selected = false;
                    VisiblilitySuggest = Visibility.Collapsed;
                    OnTextSelected?.Invoke(value);
                }
            }
        } 

        public Action<string> OnTextSelected { get; set; }

        private string searchText = String.Empty;
        public string SearchText
        {
            get => searchText;
            set 
            {
                searchText = value;
                if (string.IsNullOrEmpty(searchText) || Selected)
                {
                    VisiblilitySuggest = Visibility.Collapsed;
                    Suggestions.Clear();
                    return;
                }
                selectedText = "";
                VisiblilitySuggest = Visibility.Visible;
                UpdateSuggestions();
            }
        } 
        public ICommand SearchCommand { get; set; }

        public void AddData(string Data)=> Trie.AddWord(Data);

        public void RemoveData(string Data) =>Trie.RemoveWord(Data);
        public ModelTrie()
        {
            Suggestions = new ObservableCollection<string>();
            SearchCommand = new RelayCommand<object>(ExecuteSearch);
        }
        public void SetWords(params string[] list_str)
        {
            Trie.Clear();
            foreach (string word in list_str)
                Trie.AddWord(word);
        }
        public bool HasValue(string value)
            =>Trie.HasWord(value);
        private void ExecuteSearch(object? parameter)
        {
            if (string.IsNullOrEmpty(searchText))
                return;
            Suggestions.Clear();
            foreach(var str in Trie.GetWords(searchText))
                Suggestions.Add(str);
        }

        private void UpdateSuggestions()
        {
            string target = searchText.ToLower();
            ExecuteSearch(null);
        }
    }
}
