using System;
using System.Collections.Generic;
using System.Xml.Linq;
using SeaOfThieves.Exceptions;

namespace Bot_NetCore.Entities
{
    public class Vote
    {
        public static Dictionary<string, Vote> Votes = new Dictionary<string, Vote>();

        public readonly string Id;

        private string _topic;
        private int _yes;
        private int _no;
        private DateTime _end;
        private ulong _message;
        private ulong _author;
        private Dictionary<ulong, bool> _voters;

        public string Topic
        {
            get => _topic;
            set
            {
                _topic = value;
                Votes[Id]._topic = value;
            }
        }

        public int Yes
        {
            get => _yes;
            set
            {
                _yes = value;
                Votes[Id]._yes = value;
            }
        }

        public int No
        {
            get => _no;
            set
            {
                _no = value;
                Votes[Id]._no = value;
            }
        }

        public DateTime End
        {
            get => _end;
            set
            {
                _end = value;
                Votes[Id]._end = value;
            }
        }

        public ulong Message
        {
            get => _message;
            set
            {
                _message = value;
                Votes[Id]._message = value;
            }
        }

        public ulong Author
        {
            get => _author;
            set
            {
                _author = value;
                Votes[Id]._author = value;
            }
        }

        public Dictionary<ulong, bool> Voters
        {
            get => _voters;
            set
            {
                _voters = value;
                Votes[Id]._voters = value;
            }
        }

        public Vote(string topic, int yes, int no, DateTime end, ulong message, ulong author, string id, Dictionary<ulong, bool> voters)
        {
            Id = id;

            _topic = topic;
            _yes = yes;
            _no = no;
            _end = end;
            _message = message;
            _author = author;
            _voters = voters;

            Votes[Id] = this;
        }

        public static Vote GetByMessageId(ulong messageId)
        {
            foreach (var vote in Votes.Values)
            {
                if (vote.Message == messageId)
                {
                    return vote;
                }
            }

            throw new NotFoundException("Vote not found!");
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
                    votersEl.Add(new XElement("Voter", voter.Key, new XAttribute("vote", voter.Value)));
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
                var voters = new Dictionary<ulong, bool>();

                foreach (var voterEl in voteEl.Element("Voters")?.Elements())
                    voters.Add(Convert.ToUInt64(voterEl.Value),
                        voteEl.Attribute("vote") != null ? Convert.ToBoolean(voteEl.Attribute("vote").Value) : false);

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