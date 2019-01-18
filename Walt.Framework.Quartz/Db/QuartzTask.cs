
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Walt.Framework.Quartz
{
    [Table("p_Task")]
    public class QuartzTask
    {
        public string TaskID{get;set;}
        public string TaskName{get;set;}
        public string GroupName{get;set;}
        public string TaskParam{get;set;}
        public string CronExpressionString{get;set;}
        public string AssemblyName{get;set;}
        public string ClassName{get;set;}
        public int Status{get;set;}
        public int? OperateStatus{get;set;}
        public int IsDelete{get;set;}
        public DateTime CreatedTime{get;set;}
        public DateTime? ModifyTime{get;set;}
        public DateTime? RecentRunTime{get;set;}
        public DateTime? NextFireTime{get;set;}
        public string CronRemark{get;set;}
        public string Remark{get;set;}


    }
}