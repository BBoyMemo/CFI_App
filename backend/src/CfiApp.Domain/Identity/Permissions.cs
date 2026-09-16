namespace CfiApp.Domain.Identity;

/// <summary>
/// The complete list of capabilities in the system, and which role holds which.
///
/// This lives in the domain because it is the authorisation contract: controllers refer
/// to these constants, the seeder writes them to the database, and a test asserts the two
/// stay in step. Nothing checks a role name.
/// </summary>
public static class Permissions
{
    public const string UserApprove = "user.approve";
    public const string UserManage = "user.manage";

    public const string WorkOrderCreate = "workorder.create";
    public const string WorkOrderViewOwn = "workorder.viewOwn";
    public const string WorkOrderViewAll = "workorder.viewAll";
    public const string WorkOrderHistory = "workorder.history";
    public const string WorkOrderClaim = "workorder.claim";
    public const string WorkOrderAssign = "workorder.assign";
    public const string WorkOrderClose = "workorder.close";
    public const string WorkOrderReject = "workorder.reject";

    public const string QaCheck = "qa.check";
    public const string QaSignOff = "qa.signOff";
    public const string ProductionSignOff = "production.signOff";

    public const string TaskViewAssigned = "task.viewAssigned";
    public const string TaskComplete = "task.complete";
    public const string TaskManage = "task.manage";

    public const string OrderCreate = "order.create";
    public const string OrderManageAll = "order.manageAll";

    public const string AttendanceClock = "attendance.clock";
    public const string AttendanceViewOwn = "attendance.viewOwn";
    public const string AttendanceViewTeam = "attendance.viewTeam";
    public const string AttendanceCorrect = "attendance.correct";

    public const string OvertimeDeclare = "overtime.declare";
    public const string OvertimeApprove = "overtime.approve";

    public const string HolidayRequest = "holiday.request";
    public const string HolidayApprove = "holiday.approve";

    public const string ShiftViewOwn = "shift.viewOwn";
    public const string ShiftPlan = "shift.plan";

    public const string MessageRead = "message.read";
    public const string MessageSend = "message.send";

    public const string AdminManage = "admin.manage";

    /// <summary>Human readable description for each key, shown in the admin panel.</summary>
    public static readonly IReadOnlyDictionary<string, string> Catalog = new Dictionary<string, string>
    {
        [UserApprove] = "Approve pending registrations and assign role, department and units",
        [UserManage] = "Edit and disable user accounts",
        [WorkOrderCreate] = "Report a breakdown",
        [WorkOrderViewOwn] = "See breakdowns the user reported",
        [WorkOrderViewAll] = "See breakdowns reported by other people",
        [WorkOrderHistory] = "Search closed breakdown history and export it for an audit",
        [WorkOrderClaim] = "Take a breakdown from the pool",
        [WorkOrderAssign] = "Assign or reassign a breakdown to an engineer",
        [WorkOrderClose] = "Submit the closure form for a breakdown",
        [WorkOrderReject] = "Turn down a report that is not a fault, a duplicate, or not ours",
        [QaCheck] = "Record the swab test result for intrusive work",
        [QaSignOff] = "Sign off intrusive work on behalf of QA",
        [ProductionSignOff] = "Sign off that the area is clean and released back into service",
        [TaskViewAssigned] = "See tasks assigned to the user",
        [TaskComplete] = "Complete an assigned task",
        [TaskManage] = "Create, assign and delete tasks",
        [OrderCreate] = "Raise a part order request",
        [OrderManageAll] = "See all order requests and mark them ordered",
        [AttendanceClock] = "Clock in and out",
        [AttendanceViewOwn] = "See own hours and overtime",
        [AttendanceViewTeam] = "See hours for the team the user is responsible for",
        [AttendanceCorrect] = "Record a correction against a clock event",
        [OvertimeDeclare] = "Declare own overtime",
        [OvertimeApprove] = "Approve or reject overtime",
        [HolidayRequest] = "Request holiday",
        [HolidayApprove] = "Approve or reject holiday requests",
        [ShiftViewOwn] = "See own shifts",
        [ShiftPlan] = "Create shift types and plan the rota",
        [MessageRead] = "Read company messages",
        [MessageSend] = "Send messages to people or departments",
        [AdminManage] = "Manage units, areas, lines, equipment, occupations and roles"
    };

    public static class Roles
    {
        public const string Operator = "Operator";
        public const string Supervisor = "Supervisor";
        public const string FltDriver = "FltDriver";
        public const string Engineer = "Engineer";
        public const string MaintenanceManager = "MaintenanceManager";
        public const string Qa = "QA";
        public const string QaManager = "QaManager";
        public const string ProductionManager = "ProductionManager";
    }

    /// <summary>
    /// Which side of the site each role works on. Nobody picks this on a form: the role
    /// already says it, and asking twice only creates a way to get it wrong. An operator
    /// runs a line, so they are Production; a engineer fixes it, so they are Maintenance.
    ///
    /// QA is its own department now that the site has a QA Manager. It used to sit under
    /// Maintenance for one reason only - there was nobody else to approve them or answer
    /// to - and leaving it there would have meant the QA Manager seeing every engineer as
    /// their team, and the Maintenance Manager seeing every QA as theirs.
    ///
    /// FLT drivers are their own department. They move stock for both sides of the site
    /// rather than running a line, so filing them under Production would put them in a
    /// team they do not belong to.
    ///
    /// One consequence, and it is deliberate: a manager sees their own department plus
    /// whatever ManagerScope grants them, so the Production Manager approves FLT drivers
    /// but does not see them on the team screen until an admin gives them a scope over
    /// the FLT department. That is a data decision for the site, not a rule in code.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RoleDepartments =
        new Dictionary<string, string>
        {
            [Roles.Operator] = Departments.Production,
            [Roles.Supervisor] = Departments.Production,
            [Roles.FltDriver] = Departments.Flt,
            [Roles.ProductionManager] = Departments.Production,
            [Roles.Engineer] = Departments.Maintenance,
            [Roles.MaintenanceManager] = Departments.Maintenance,
            [Roles.Qa] = Departments.Qa,
            [Roles.QaManager] = Departments.Qa
        };

    public static class Departments
    {
        public const string Production = "Production";
        public const string Maintenance = "Maintenance";

        /// <summary>Forklift drivers. Their own department, not part of production.</summary>
        public const string Flt = "FLT";

        /// <summary>Quality assurance, under its own manager.</summary>
        public const string Qa = "QA";
    }

    /// <summary>
    /// Who an approver is allowed to let in, and as what. Approving someone is delegation,
    /// not just a permission check: a Maintenance Manager runs the engineers and, as the
    /// site's admin, is the one who lets in every other manager. Each manager then takes on
    /// their own people - a QA reports to the QA Manager, an operator to the Production
    /// Manager, and neither sits under maintenance, so maintenance does not hand out those
    /// roles. The admin lets the QA Manager in and stops there; it is the QA Manager who
    /// decides who does QA work.
    ///
    /// A role missing from this map can approve nobody, which is the safe default: adding
    /// user.approve to a role by mistake does not quietly make it an admin.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> ApprovableRoles =
        new Dictionary<string, string[]>
        {
            [Roles.MaintenanceManager] =
            [
                Roles.Engineer, Roles.MaintenanceManager,
                Roles.QaManager, Roles.ProductionManager
            ],

            [Roles.QaManager] = [Roles.Qa],

            [Roles.ProductionManager] = [Roles.Operator, Roles.Supervisor, Roles.FltDriver]
        };

    private static readonly string[] EveryoneBaseline =
    [
        AttendanceClock, AttendanceViewOwn, OvertimeDeclare,
        ShiftViewOwn, MessageRead, WorkOrderCreate, WorkOrderViewOwn
    ];

    private static readonly string[] EngineerCore =
    [
        WorkOrderViewAll, WorkOrderHistory, WorkOrderClaim, WorkOrderClose,
        TaskViewAssigned, TaskComplete, OrderCreate
    ];

    private static readonly string[] QaCore =
    [
        WorkOrderViewAll, QaCheck, QaSignOff
    ];

    private static readonly string[] ManagerCore =
    [
        UserApprove, UserManage, AttendanceViewTeam, AttendanceCorrect,
        OvertimeApprove, HolidayApprove, ShiftPlan, MessageSend
    ];

    /// <summary>
    /// Role to permission map. A manager role is deliberately a superset of the role it
    /// runs: the Maintenance Manager repairs machines as well as running the team, and the
    /// QA Manager swabs as well as running QA.
    /// Managers do not hold HolidayRequest - they approve leave, they do not book it here.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> RoleGrants =
        new Dictionary<string, string[]>
        {
            [Roles.Operator] = [.. EveryoneBaseline, HolidayRequest],

            // Both new roles start on the floor baseline: clock in, report a breakdown,
            // book holiday. What else they may do is a decision the site has not made yet,
            // and an unearned permission is harder to take back than to add.
            [Roles.Supervisor] = [.. EveryoneBaseline, HolidayRequest],
            [Roles.FltDriver] = [.. EveryoneBaseline, HolidayRequest],

            [Roles.Engineer] = [.. EveryoneBaseline, HolidayRequest, .. EngineerCore],

            [Roles.MaintenanceManager] =
            [
                .. EveryoneBaseline, .. EngineerCore, .. ManagerCore,
                WorkOrderAssign, WorkOrderReject, TaskManage, OrderManageAll, AdminManage
            ],

            [Roles.Qa] = [.. EveryoneBaseline, HolidayRequest, .. QaCore],

            [Roles.QaManager] = [.. EveryoneBaseline, .. QaCore, .. ManagerCore],

            [Roles.ProductionManager] =
            [
                .. EveryoneBaseline, .. ManagerCore,
                WorkOrderViewAll, ProductionSignOff
            ]
        };
}
