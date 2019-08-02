using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data_struct
{
    [Serializable]
    class Data
    {
        //마우스 X,Y 데이터
        //클릭 버튼
        public int X, Y;
        public int mode; //mode : 1 왼쪽클릭 2 오른쪽클릭 3 휠클릭 4 더블클릭
    }
}
