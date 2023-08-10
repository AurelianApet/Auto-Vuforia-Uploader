using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VuforiaUpload
{
    public class Target
    {
        public const string STATUS_ACTIVE = "Active";
        public const string STATUS_INACTIVE = "Inactive";
        public const string STATUS_PENDING = "Pending";

        private string _id = "";
        private string _name = "";
        //private int _recog_rate = 0;
        private int _augment_rate = 0;
        private string _status = "";

        public string ID
        {
            get { return _id; }
        }
        public string Name
        {
            get { return _name; }
        }
        //public int RecogRate
        //{
        //    get { return _recog_rate; }
        //}
        public int AugmentRate
        {
            get { return _augment_rate; }
        }
        public string Status
        {
            get { return _status; }
        }

        public Target(string strID, string strName, /*int iRecogRate, */int iAugmentRate, string strStatus)
        {
            _id = strID;
            _name = strName;
            //_recog_rate = iRecogRate;
            _augment_rate = iAugmentRate;
            _status = strStatus;
        }
    }
}
