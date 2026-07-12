using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AqualLifeStyle.Authorization
{
    public static class AquaPermissions
    {
        public static class Members
        {
            public const string Default = "Aqua.Members";
            public const string View = Default + ".View";
            public const string Create = Default + ".Create";
            public const string Edit = Default + ".Edit";
            public const string Delete = Default + ".Delete";
            public const string Upgrade = Default + ".Upgrade";
            public const string ViewSelf = Default + ".ViewSelf";
            public const string EditSelf = Default + ".EditSelf";
        }

        public static class Facilitators
        {
            public const string Default = "Aqua.Facilitators";
            public const string View = Default + ".View";
            public const string Register = Default + ".Register";
            public const string Refer = Default + ".Refer";
            public const string Promote = Default + ".Promote";
            public const string ViewSelf = Default + ".ViewSelf";
        }

        public static class AreaLeaders
        {
            public const string Default = "Aqua.AreaLeaders";
            public const string View = Default + ".View";
            public const string Apply = Default + ".Apply";
            public const string Approve = Default + ".Approve";
            public const string Manage = Default + ".Manage";
            public const string ViewSelf = Default + ".ViewSelf";
        }

        public static class AreaSpaces { public const string Default = "Aqua.AreaSpaces"; public const string View = Default + ".View"; public const string Apply = Default + ".Apply"; public const string Approve = Default + ".Approve"; public const string Manage = Default + ".Manage"; }
        public static class Orders { public const string Default = "Aqua.Orders"; public const string View = Default + ".View"; public const string Place = Default + ".Place"; public const string Process = Default + ".Process"; public const string Approve = Default + ".Approve"; public const string ViewSelf = Default + ".ViewSelf"; }
        public static class Savings { public const string Default = "Aqua.Savings"; public const string View = Default + ".View"; public const string Deposit = Default + ".Deposit"; public const string Withdraw = Default + ".Withdraw"; public const string Approve = Default + ".Approve"; public const string ViewSelf = Default + ".ViewSelf"; }
        public static class Enquiries { public const string Default = "Aqua.Enquiries"; public const string View = Default + ".View"; public const string Create = Default + ".Create"; public const string Update = Default + ".Update"; public const string Resolve = Default + ".Resolve"; public const string ViewSelf = Default + ".ViewSelf"; }
        public static class Referrals { public const string Default = "Aqua.Referrals"; public const string View = Default + ".View"; public const string Create = Default + ".Create"; public const string Confirm = Default + ".Confirm"; public const string ViewSelf = Default + ".ViewSelf"; }
        public static class Admin { public const string Default = "Aqua.Admin"; public const string Dashboard = Default + ".Dashboard"; public const string Reports = Default + ".Reports"; public const string Audit = Default + ".Audit"; public const string Settings = Default + ".Settings"; public const string AllTenants = Default + ".AllTenants"; }

        private static readonly Lazy<IReadOnlyCollection<string>> AllPermissionNames =
            new Lazy<IReadOnlyCollection<string>>(() => typeof(AquaPermissions).GetNestedTypes(BindingFlags.Public)
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
                .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue())
                .Distinct(StringComparer.Ordinal)
                .ToArray());

        public static IReadOnlyCollection<string> GetAll() => AllPermissionNames.Value;
    }
}
