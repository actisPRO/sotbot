using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    public class Vote
    {
        public static Dictionary<ulong, Vote> Votes = new Dictionary<ulong, Vote>();

        public string Topic;
        public int Yes;
        public int No;
        public DateTime End;
        public ulong Message;
        public ulong Author;
        public string Id;
        public List<ulong> Voters;

        public Vote(string topic, int yes, int no, DateTime end, ulong message, ulong author, string id, List<ulong> voters)
        {
            Topic = topic;
            Yes = yes;
            No = no;
            End = end;
            Message = message;
            Author = author;
            Id = id;
            Voters = voters;
            
            Votes[message] = this;
        }

        public static void Save(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("Votes");

            foreach (var vote in Votes.Values)
            {
                var newEl = new XElement("Vote");
                newEl.Add(new XElement("Topic", vote.Topic));
                newEl.Add(new XElement("Yes", vote.Yes));
                newEl.Add(new XElement("No", vote.No));
                newEl.Add(new XElement("End", vote.End.ToString("s")));
                newEl.Add(new XElement("Message", vote.Message));
                newEl.Add(new XElement("Author", vote.Author));
                newEl.Add(new XElement("Id", vote.Id));

                var votersEl = new XElement("Voters");
                foreach (var voter in vote.Voters)
                {
                    votersEl.Add(new XElement("Voter", voter));
                }
                newEl.Add(votersEl);
                
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
                var voters = new List<ulong>();
                foreach (var voterEl in voteEl.Element("Voters")?.Elements())
                {
                    voters.Add(Convert.ToUInt64(voterEl.Value));
                }
                
                var vote = new Vote(voteEl.Element("Topic").Value, 
                    Convert.ToInt32(voteEl.Element("Yes").Value),
                    Convert.ToInt32(voteEl.Element("No").Value),
                    Convert.ToDateTime(voteEl.Element("End").Value),
                    Convert.ToUInt64(voteEl.Element("Message").Value),
                    Convert.ToUInt64(voteEl.Element("Author").Value),
                    voteEl.Element("Id").Value,
                    voters);
            }
        }
    }
}