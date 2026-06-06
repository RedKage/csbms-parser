using System;
using System.Collections.Generic;
using System.Text;

namespace CSBMSParser
{
    public class EventLane
    {
        private TimeLine[] sections;
        private int sectionbasepos;
        private int sectionseekpos;

        private TimeLine[] bpms;
        private int bpmbasepos;
        private int bpmseekpos;

        private TimeLine[] stops;
        private int stopbasepos;
        private int stopseekpos;

        public EventLane(BMSModel model)
        {
            TimeLine[] timelines = model.getAllTimeLines();
            int sectionCount = 0;
            int bpmCount = 0;
            int stopCount = 0;

            TimeLine prev = null;
            foreach (TimeLine tl in timelines)
            {
                if (tl.getSectionLine())
                {
                    sectionCount++;
                }
                if (tl.getBPM() != (prev != null ? prev.getBPM() : model.getBpm()))
                {
                    bpmCount++;
                }
                if (tl.getStop() != 0)
                {
                    stopCount++;
                }
                prev = tl;
            }

            sections = new TimeLine[sectionCount];
            bpms = new TimeLine[bpmCount];
            stops = new TimeLine[stopCount];

            int sectionIndex = 0;
            int bpmIndex = 0;
            int stopIndex = 0;
            prev = null;
            foreach (TimeLine tl in timelines)
            {
                if (tl.getSectionLine())
                {
                    sections[sectionIndex] = tl;
                    sectionIndex++;
                }
                if (tl.getBPM() != (prev != null ? prev.getBPM() : model.getBpm()))
                {
                    bpms[bpmIndex] = tl;
                    bpmIndex++;
                }
                if (tl.getStop() != 0)
                {
                    stops[stopIndex] = tl;
                    stopIndex++;
                }
                prev = tl;
            }
        }

        public TimeLine[] getSections()
        {
            return sections;
        }

        public TimeLine[] getBpmChanges()
        {
            return bpms;
        }

        public TimeLine[] getStops()
        {
            return stops;
        }

        public TimeLine getSection()
        {
            if (sectionseekpos < sections.Length)
            {
                return sections[sectionseekpos++];
            }
            return null;
        }

        public TimeLine getBpm()
        {
            if (bpmseekpos < bpms.Length)
            {
                return bpms[bpmseekpos++];
            }
            return null;
        }

        public TimeLine getStop()
        {
            if (stopseekpos < stops.Length)
            {
                return stops[stopseekpos++];
            }
            return null;
        }

        public void reset()
        {
            sectionseekpos = sectionbasepos;
            bpmseekpos = bpmbasepos;
            stopseekpos = stopbasepos;
        }

        public void mark(int time)
        {
            for (; sectionbasepos < sections.Length - 1 && sections[sectionbasepos + 1].getMilliTime() > time; sectionbasepos++)
                ;
            for (; sectionbasepos > 0 && sections[sectionbasepos].getMilliTime() < time; sectionbasepos--)
                ;
            for (; bpmbasepos < bpms.Length - 1 && bpms[bpmbasepos + 1].getMilliTime() > time; bpmbasepos++)
                ;
            for (; bpmbasepos > 0 && bpms[bpmbasepos].getMilliTime() < time; bpmbasepos--)
                ;
            for (; stopbasepos < stops.Length - 1 && stops[stopbasepos + 1].getMilliTime() > time; stopbasepos++)
                ;
            for (; stopbasepos > 0 && stops[stopbasepos].getMilliTime() < time; stopbasepos--)
                ;
            sectionseekpos = sectionbasepos;
            bpmseekpos = bpmbasepos;
            stopseekpos = stopbasepos;
        }


    }

}
