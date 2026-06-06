using System;
using System.Collections.Generic;
using System.Text;

namespace CSBMSParser
{
    public class Lane
    {
        private Note[] notes;
        private int notebasepos;
        private int noteseekpos;

        private Note[] hiddens;
        private int hiddenbasepos;
        private int hiddenseekpos;

        internal Lane(Note[] notes, Note[] hiddens)
        {
            this.notes = notes;
            this.hiddens = hiddens;
        }

        public Lane(BMSModel model, int lane)
        {
            TimeLine[] timelines = model.getAllTimeLines();
            int noteCount = 0;
            int hiddenCount = 0;

            foreach (TimeLine tl in timelines)
            {
                if (tl.getNote(lane) != null)
                {
                    noteCount++;
                }
                if (tl.getHiddenNote(lane) != null)
                {
                    hiddenCount++;
                }
            }

            notes = new Note[noteCount];
            hiddens = new Note[hiddenCount];

            int noteIndex = 0;
            int hiddenIndex = 0;
            foreach (TimeLine tl in timelines)
            {
                Note note = tl.getNote(lane);
                if (note != null)
                {
                    notes[noteIndex] = note;
                    noteIndex++;
                }

                Note hidden = tl.getHiddenNote(lane);
                if (hidden != null)
                {
                    hiddens[hiddenIndex] = hidden;
                    hiddenIndex++;
                }
            }
        }

        public Note[] getNotes()
        {
            return notes;
        }

        public Note[] getHiddens()
        {
            return hiddens;
        }

        public Note getNote()
        {
            if (noteseekpos < notes.Length)
            {
                return notes[noteseekpos++];
            }
            return null;
        }

        public Note getHidden()
        {
            if (hiddenseekpos < hiddens.Length)
            {
                return hiddens[hiddenseekpos++];
            }
            return null;
        }

        public void reset()
        {
            noteseekpos = notebasepos;
            hiddenseekpos = hiddenbasepos;
        }

        public void mark(int time)
        {
            for (; notebasepos < notes.Length - 1 && notes[notebasepos + 1].getTime() < time; notebasepos++)
                ;
            for (; notebasepos > 0 && notes[notebasepos].getTime() > time; notebasepos--)
                ;
            noteseekpos = notebasepos;
            for (; hiddenbasepos < hiddens.Length - 1
                    && hiddens[hiddenbasepos + 1].getTime() < time; hiddenbasepos++)
                ;
            for (; hiddenbasepos > 0 && hiddens[hiddenbasepos].getTime() > time; hiddenbasepos--)
                ;
            hiddenseekpos = hiddenbasepos;
        }
    }

}
