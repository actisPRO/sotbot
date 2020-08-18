using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public class Note
    {
        public static Dictionary<ulong, Note> Notes = new Dictionary<ulong, Note>();
        
        public readonly ulong User;

        private string _content;
        public string Content
        {
            get => _content;
            set
            {
                _content = value;
                Notes[User]._content = value;
            }
        }

        public Note(ulong user, string content)
        {
            User = user;
            Content = content;

            Notes[User] = this;
        }

        public static void Read(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Save(fileName);
                return;
            }

            var doc = XDocument.Load(fileName);
            var root = doc.Root;

            foreach (var noteEl in root.Elements())
            {
               var note = new Note(Convert.ToUInt64(noteEl.Attribute("user").Value), noteEl.Value);
            }
        }
        
        public static void Save(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("Notes");

            foreach (var note in Notes.Values)
            {
                root.Add(new XElement("Note", note.Content, new XAttribute("user", note.User)));
            }
            
            doc.Add(root);
            doc.Save(fileName);
        }
    }
}