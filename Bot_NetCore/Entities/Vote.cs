using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public class Vote
    {
        public static Dictionary<string, Vote> Votes = new Dictionary<string, Vote>();

        public string Topic;
        public DateTime End;
        public ulong Message;
        public ulong Author;
        public string Id;

        public Vote(string topic, DateTime end, ulong message, ulong author, string id)
        {
            Topic = topic;
            End = end;
            Message = message;
            Author = author;
            Id = id;
            
            Votes[id] = this;
        }

        public static void Save(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("Votes");

            foreach (var vote in Votes.Values)
            {
                var newEl = new XElement("Vote");
                newEl.Add(new XElement("Topic", vote.Topic));
                newEl.Add(new XElement("End", vote.End.ToString("s")));
                newEl.Add(new XElement("Message", vote.Message));
                newEl.Add(new XElement("Author", vote.Author));
                newEl.Add(new XElement("Id", vote.Id));
                root.Add(newEl);
            }
            
            doc.Add(root);
            doc.Save(fileName);
        }

        public static void Read(string fileName)
        {
            var doc = XDocument.Load(fileName);
            var root = doc.Root;

            foreach (var voteEl in root.Elements())
            {
                var vote = new Vote(voteEl.Element("Topic").Value, 
                    Convert.ToDateTime(voteEl.Element("End").Value),
                    Convert.ToUInt64(voteEl.Element("Message").Value),
                    Convert.ToUInt64(voteEl.Element("Author").Value),
                    voteEl.Element("Id").Value);
            }
        }
    }
}