using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public static partial class CoreGameplayChecks
{
    private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
    private static readonly Type Motel = Type("MotelService");
    private static readonly Type Portfolio = Type("CountyPortfolio");
    private static int assertions;

    private static Type Type(string name) => Game.GetType("DesertLantern." + name, true);
    private static object EnumValue(string type, string item) => Enum.Parse(Type(type), item);
    private static object NewMotel() => Motel.GetMethod("New").Invoke(null, null);
    private static object Call(object target, string method, params object[] args)
    {
        var signature = args.Select(arg => arg.GetType()).ToArray();
        return target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, signature, null).Invoke(target, args);
    }
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static int Number(object target, string name) => Convert.ToInt32(Field(target, name).GetValue(target));
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static bool Yes(object target, string method, params object[] args) => (bool)Call(target, method, args);
    private static int Report(object service, string account) =>
        (int)Call(service, "ReportAmount", 1, EnumValue("Account", account));
    private static bool Book(object service, int room, int start, int end, string source = "Online") =>
        Yes(service, "Book", room, start, end, EnumValue("BookingChannel", source), EnumValue("BookingPreference", "None"));

    private static bool CheckInReserved(object service, int room, int arrivedAt, int deskMinute, bool staffed = false)
    {
        int start = Number(service, "ClockMinute");
        int end = (start / 1440 + 1) * 1440 + 660;
        return Book(service, room, start, end) && Yes(service, staffed ? "CheckInWithStaff" : "CheckIn", room, arrivedAt, deskMinute);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException("Core gameplay check failed: " + name);
        assertions++;
    }

    public static void Run()
    {
        assertions = 0;
        Reservations();
        OnlineApproval();
        DeskCheckout();
        Phone();
        Requests();
        Supplies();
        Housekeeping();
        StaffingAndFinances();
        Routing();
        BookedVehicleArrivals();
        RequestsAndBilling();
        Integration();
        Debug.Log($"Core gameplay checks passed ({assertions} assertions). Physical playthrough and live LAN remain separate.");
    }

    private static void Reservations()
    {
        object service = NewMotel();
        Check((int)Property(service, "ReservationCount") == 0, "new motel has no preloaded reservations");
        Check(Book(service, 0, 960, 3840, "Telephone"), "telephone reservation");
        Check(!Book(service, 0, 970, 1550), "reject overlapping dates");
        Check(Book(service, 0, 3840, 4200), "allow adjacent booking");
        Call(service, "ModifyBooking", 1, 0);
        Check(Book(service, 0, 3840, 4200), "cancel makes dates available");
        Call(service, "ModifyBooking", 1, 5);
        Check((int)Call(service, "ReservationRoom", 1) == 1 &&
            (int)Call(service, "ReservationStart", 1) == 3840, "move preserves dates");
        Check(Call(service, "ReservationChannel", 0).ToString() == "Telephone", "preserve calendar channel");
        Set(service, "ClockMinute", 959);
        Check(!Yes(service, "BookingDue", 0), "arrival held until start");
        Set(service, "ClockMinute", 960);
        Check(Yes(service, "BookingDue", 0), "arrival due at start");
        Check(Yes(service, "CheckIn", 0, 960, 960), "booked guest checks in");
        Check(Report(service, "RoomRevenue") == 130, "two nights billed at booked rate");
        Check(!Yes(service, "CheckInWithStaff", 0, 960, 960) && Report(service, "RoomRevenue") == 130, "same check-in cannot charge twice");
        Check(Book(service, 0, 3850, 5000), "future reservation after departure");
        Call(service, "ExtendStay", 0);
        Check((int)Call(service, "DepartureMinute", 0) == 3840, "extension cannot displace booking");
        Call(service, "AdvanceClock", 3840);
        Check((int)Call(service, "CheckoutWaiting") == 0, "checkout triggers on departure");
        Call(service, "CompleteCheckout", 0, true);
        Check(!Yes(service, "ArrivalReady", 0), "dirty turnover blocks check-in");

        service = NewMotel();
        Check((int)Call(service, "NextArrival") == -1 &&
            !Yes(service, "CheckIn", 0, 720, 720), "unscheduled walk-in cannot spawn or check in");
        Check(CheckInReserved(service, 0, 720, 720), "new reservation enables check-in");
        for (int room = 1; room < 8; room++)
            Call(service, "ToggleRoomClosure", room);
        Check((int)Call(service, "NextArrival") == -1 && !Book(service, 4, 800, 950), "no assignable room");
        service = NewMotel();
        Call(service, "AdvanceClock", 1440);
        Check((int)Property(service, "ReservationCount") > 0, "online booking generated on day turnover");
        service = NewMotel();
        for (int room = 0; room < 8; room++)
            Check(Book(service, room, 3200, 4700), $"room {room + 1} can be booked in the same date block");
        Check(Enumerable.Range(0, 8).All(room => !Book(service, room, 3300, 4500)),
            "fully booked date block rejects all overlapping requests");
    }

    private static void OnlineApproval()
    {
        object service = NewMotel();
        Call(service, "AdvanceClock", 1440);
        int start = (int)Call(service, "ReservationStart", 0);
        int room = (int)Call(service, "ReservationRoom", 0);
        Check(Yes(service, "ReservationPending", 0) && (int)Call(service, "ReservationRate", 0) == 65,
            "incoming online booking awaits approval and preserves its rate");
        Call(service, "AdvanceClock", start);
        Check(!Yes(service, "BookingDue", room), "unapproved reservation cannot spawn a guest");
        Check(Yes(service, "AcceptReservation", 0, start) && Yes(service, "BookingDue", room) &&
            !Yes(service, "AcceptReservation", 0, start), "accepted reservation can spawn once its scheduled time arrives");
    }

    private static void DeskCheckout()
    {
        object service = NewMotel();
        Check(CheckInReserved(service, 0, 720, 720), "desk checkout fixture");
        int departure = (int)Call(service, "DepartureMinute", 0);
        int guestId = (int)Call(service, "GuestId", 0);
        Check(!Yes(service, "TryDeskCheckout", 0, (float)departure, (float)guestId),
            "a checkout before the scheduled departure is rejected");
        Set(service, "ClockMinute", departure);
        Check(!Yes(service, "TryDeskCheckout", 0, (float)departure, (float)(guestId + 1)),
            "a checkout for an earlier guest cannot settle the current stay");
        Check(Yes(service, "TryDeskCheckout", 0, (float)departure, (float)guestId) &&
            !Yes(service, "TryDeskCheckout", 0, (float)departure, (float)guestId) &&
            !Yes(service, "RoomReadyForGuest", 0), "desk checkout settles once and creates housekeeping work");
    }

    private static void Phone()
    {
        object request = NewMotel();
        Call(request, "AdvanceClock", Number(request, "NextPhoneCall"));
        Check(Yes(request, "TryAnswerPhone", 1ul), "bookings desk receives the reservation call");
        Check(!Yes(request, "QueuePhoneReservation", 0, 960, 2400, EnumValue("BookingPreference", "None"), 2ul) &&
            Yes(request, "QueuePhoneReservation", 0, 960, 2400, EnumValue("BookingPreference", "None"), 1ul) &&
            (int)Property(request, "PendingReservationCount") == 1 &&
            Call(request, "ReservationChannel", 0).ToString() == "Telephone", "phone request enters the shared confirmation queue once");
        Call(request, "AdvanceClock", 960);
        Check((int)Call(request, "NextArrival") == -1 && Yes(request, "AcceptReservation", 0, 960) &&
            (int)Call(request, "NextArrival") == 0, "phone guest arrives only after confirmation");

        object service = NewMotel();
        int call = Number(service, "NextPhoneCall");
        Call(service, "AdvanceClock", call);
        Check((bool)Property(service, "PhoneRinging"), "incoming call signal");
        Check(Yes(service, "TryAnswerPhone", 1ul), "first caller answers");
        Check(!Yes(service, "TryAnswerPhone", 2ul) && !Yes(service, "CanBookPhoneFor", 2ul), "second caller cannot claim booking");
        Check(!Yes(service, "DeclinePhone", 2ul) && Yes(service, "DeclinePhone", 1ul), "only owner can decline active call");
        Check(Number(service, "MissedCalls") == 1 && (bool)Property(service, "CanReturnBookingCall"), "declined booking is in missed history");
        Check(Yes(service, "ReturnBookingCall", 2ul) && Yes(service, "CanBookPhoneFor", 2ul), "callback has exclusive ownership");
        Call(service, "ReleasePhone", 2ul);
        Check(Number(service, "MissedCalls") == 2, "disconnect releases call to history");
        int next = Number(service, "NextPhoneCall");
        Call(service, "AdvanceClock", next + 55);
        Check(Number(service, "MissedCalls") == 3, "unanswered call times out exactly once");

        service = NewMotel();
        Set(service, "Cash", 500);
        Call(service, "ManageStaff", 5, false);
        Call(service, "AdvanceClock", Number(service, "NextPhoneCall"));
        Call(service, "WorkMotelShift", false);
        Check((int)Property(service, "ReservationCount") == 1 &&
            Call(service, "ReservationChannel", 0).ToString() == "Telephone", "receptionist creates telephone booking");
    }

    private static void Requests()
    {
        object service = NewMotel();
        Check(CheckInReserved(service, 0, 720, 720), "activate guest for request");
        Call(service, "AdvanceClock", 795);
        int guest = (int)Call(service, "GuestId", 0);
        Check(Yes(service, "TryAnswerPhone", 0ul) && Yes(service, "RequestOpen", 0), "guest call opens request for correct room");
        object reference = Call(service, "RequestReference", 0);
        object item = Call(service, "KindOfRequest", 0);
        object wrong = item.ToString() == "Pillow" ? EnumValue("GuestRequestKind", "Towel") : EnumValue("GuestRequestKind", "Pillow");
        Check(!Yes(service, "TryServeRequest", 0, wrong, 1) && !Yes(service, "TryServeRequest", 0, item, 2), "wrong item or quantity rejected");
        int pillows = Number(service, "Pillows");
        Check(Yes(service, "TryServeRequest", 0, item, 1) && Number(service, "Pillows") == pillows - 1, "correct item consumed once");
        Check(!Yes(service, "TryServeRequest", 0, item, 1) &&
            ((string)Call(service, "GuestSatisfaction", 0)).Contains("Satisfied"), "completion is idempotent and affects satisfaction");
        Call(service, "AdvanceClock", 1125);
        Call(service, "AdvanceClock", 1145);
        Check((int)Call(service, "GuestId", 0) == guest && (int)Call(service, "WaitingRequest", true) == 0, "same guest generates a later request");
        Call(service, "AcknowledgeRequest", 0);
        Check(!Yes(service, "ApplyGuestRequest", reference, false), "previous request reference cannot complete a new request");
        Call(service, "AdvanceClock", 1195);
        Check((int)Call(service, "RequestWait", 0) == 50, "patient wait grows with clock");
        Call(service, "CompleteCheckout", 0, true);
        Check(!Yes(service, "RequestOpen", 0), "checkout removes open request");
    }

    private static void Supplies()
    {
        object service = NewMotel();
        int cart = 2 | 3 << 4;
        Check(Yes(service, "OrderCart", cart) && Number(service, "Cash") == 152, "bulk checkout deducts correct mixed-cart price");
        Check(Yes(service, "Order", 2) && (int)Property(service, "PendingOrderCount") == 2, "simultaneous orders");
        object saved = RoundTrip(service, "SavedState", true);
        Check((int)Property(saved, "PendingOrderCount") == 2, "pending supply orders survive binary save");
        service = saved;
        int due = Number(service, "DeliveryDue");
        Set(service, "ClockMinute", due - 1);
        Call(service, "AdvanceDelivery");
        Check(Field(service, "Delivery").GetValue(service).ToString() == "InTransit", "no early supplies");
        Set(service, "ClockMinute", due);
        Call(service, "AdvanceDelivery");
        Check(Field(service, "Delivery").GetValue(service).ToString() == "AtBay", "delivery due at scheduled minute");
        Call(service, "CollectSupply", 5ul);
        Call(service, "CollectSupply", 6ul);
        Call(service, "StoreSupply", 6ul);
        Check(Number(service, "Towels") == 0, "another worker cannot take or store the held crate");
        Call(service, "StoreSupply", 5ul);
        Call(service, "StoreSupply", 5ul);
        Check(Number(service, "CleaningKits") == 2 && Number(service, "Towels") == 12 && (int)Property(service, "PendingOrderCount") == 1,
            "correct pack quantities, no duplicate collection");
        Set(service, "Cash", 0);
        Check(!Yes(service, "Order", 0) && Number(service, "Cash") == 0, "insufficient cash cannot purchase");
        Check(Report(service, "Supplies") == -104, "supplies reconcile with expenses");
    }

    private static object RoundTrip(object service, string nested, bool useSavedState)
    {
        Type payloadType = useSavedState ? Motel.GetNestedType(nested, BindingFlags.NonPublic) : Motel;
        object payload = service;
        if (useSavedState)
        {
            payload = Activator.CreateInstance(payloadType);
            payloadType.GetField("Service").SetValue(payload, service);
        }
        object decoded = typeof(CoreGameplayChecks).GetMethod(nameof(BinaryRoundTrip), BindingFlags.Static | BindingFlags.NonPublic)
            .MakeGenericMethod(payloadType).Invoke(null, new[] { payload });
        return useSavedState ? Call(decoded, "ForProperty", EnumValue("PropertyId", "RoadsideMotel")) : decoded;
    }

    private static T BinaryRoundTrip<T>(T state) where T : INetworkSerializable, new()
    {
        using var writer = new FastBufferWriter(2048, Allocator.Temp);
        writer.WriteNetworkSerializable(state);
        using var reader = new FastBufferReader(writer, Allocator.Temp);
        reader.ReadNetworkSerializable<T>(out T value);
        return value;
    }

    private static void Housekeeping()
    {
        object service = NewMotel();
        Set(service, "TrashBags", 2);
        Set(service, "Vacuums", 1);
        Set(service, "LinenSets", 4);
        Set(service, "CleaningKits", 1);
        Check(CheckInReserved(service, 0, 720, 720), "housekeeping example guest");
        Call(service, "AdvanceClock", 900);
        Check((int)Call(service, "Cleanliness", 0) < 100, "room dirt during stay");
        var trash = EnumValue("CleaningTask", "Trash");
        Set(service, "housekeepingRequests", (ushort)1);
        Set(service, "doNotDisturb", (ushort)1);
        Check(!Yes(service, "CanClean", 0, trash), "do not disturb prevents cleaning");
        Call(service, "CompleteCheckout", 0, true);
        Check((int)Call(service, "Cleanliness", 0) == 0, "checkout puts every cleaning task on the list");
        for (int index = 0; index < 8; index++)
            Call(service, "Clean", 0, Enum.ToObject(Type("CleaningTask"), index));
        Check(Number(service, "TrashBags") == 1 && Number(service, "LinenSets") == 2 &&
            Number(service, "DirtyLinens") == 2, "housekeeping uses bags and clean linen correctly");
        Check((int)Call(service, "Cleanliness", 0) == 100 && Yes(service, "RoomReadyForGuest", 0),
            "ready only after completing the turnover list");
    }

    private static void StaffingAndFinances()
    {
        object service = NewMotel();
        Set(service, "Cash", 1500);
        Call(service, "ManageStaff", 0, false);
        Check(Yes(service, "StaffHired", 0), "employee hired");
        Check(CheckInReserved(service, 0, 720, 720) &&
            Yes(service, "PickUp", ulong.MaxValue), "bellhop claims guest luggage");
        Check(!Yes(service, "PickUp", 2ul), "second worker cannot claim same luggage");
        Call(service, "ManageStaff", 0, false);
        Check(!Yes(service, "StaffHired", 0) && Field(service, "Bag").GetValue(service).ToString() == "InCar",
            "dismissal releases unfinished luggage");
        Check(Yes(service, "PickUp", 2ul) && Yes(service, "Deliver", 2ul) && !Yes(service, "Deliver", 2ul), "delivery and tip only once");
        Call(service, "ManageStaff", 2, false);
        object synced = RoundTrip(service, null, false);
        Check(Yes(synced, "StaffHired", 2), "serialized employee assignment");
        VerifyPayrollAndLedger(synced);
    }

    private static void VerifyPayrollAndLedger(object sync)
    {
        int before = Report(sync, "Payroll");
        Call(sync, "AdvanceClock", 1440);
        int payroll = Report(sync, "Payroll");
        Check(payroll == before - 25, "daily wage charged");
        Call(sync, "AdvanceClock", 1440);
        Call(sync, "AdvanceClock", 1439);
        Check(Report(sync, "Payroll") == payroll, "repeated or backward clock cannot duplicate payroll");
        Check(Report(sync, "Electricity") < 0 && Report(sync, "Water") < 0 && Report(sync, "Internet") < 0,
            "utility billing posts to expense accounts");

        object portfolio = Activator.CreateInstance(Portfolio, BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { Activator.CreateInstance(Portfolio.GetNestedType("SaveData", BindingFlags.NonPublic), true) }, null);
        object initial = Field(Call(portfolio, "Business", 0), "Detailed").GetValue(Call(portfolio, "Business", 0));
        Check(CheckInReserved(initial, 0, 720, 720), "ledger guest check-in");
        Call(portfolio, "UpdateMotel", initial);
        var entries = ((IEnumerable)Property(portfolio, "Ledger")).Cast<object>().ToArray();
        Check(entries.Sum(entry => Number(entry, "amount")) == Number(initial, "Cash") - 230 &&
            Number(entries.Last(), "balance") == Number(initial, "Cash"), "ledger reconciles with operating balance");
    }

    private static void SetListItem(object state, string field, int index, object value)
    {
        FieldInfo member = Field(state, field);
        object list = member.GetValue(state);
        list.GetType().GetProperty("Item").SetValue(list, value, new object[] { index });
        member.SetValue(state, list);
    }

    private static void RequestsAndBilling()
    {
        object service = NewMotel();
        Set(service, "Cash", 1200);
        Set(service, "Pillows", 0);
        Call(service, "ManageStaff", 1, false);
        Check(CheckInReserved(service, 0, 720, 720), "first guest for shortage check");
        Call(service, "AdvanceClock", 795);
        Call(service, "AcknowledgeRequest", 0);
        Check(Yes(service, "RequestOpen", 0) && !Yes(service, "RequestSuppliesAvailable", 0), "low stock leaves request open");
        Call(service, "WorkMotelShift", true);
        Check(Yes(service, "RequestOpen", 0) && Number(service, "Pillows") == 0, "worker with no supplies cannot consume inventory");
        Set(service, "Pillows", 1);
        Call(service, "WorkMotelShift", true);
        Check(!Yes(service, "RequestOpen", 0) && Number(service, "Pillows") == 0, "worker completes when stock is restored");
        Set(service, "LateCheckIns", (ushort)1);
        int cash = Number(service, "Cash");
        Call(service, "RefundGuest", 0);
        Call(service, "RefundGuest", 0);
        Check(Number(service, "Cash") == cash - 15 && Report(service, "Adjustments") == -15, "refund charged once to operating finances");

        object priority = NewMotel();
        Check(CheckInReserved(priority, 0, 720, 720), "first request in priority fixture");
        Check(CheckInReserved(priority, 3, 720, 720, true), "second request in priority fixture");
        Call(priority, "AdvanceClock", 820);
        SetListItem(priority, "requestKinds", 3, (byte)3);
        SetListItem(priority, "requestMinutes", 3, 815);
        Call(priority, "AcknowledgeRequest", 0);
        Call(priority, "AcknowledgeRequest", 3);
        Call(priority, "ManageStaff", 1, false);
        Call(priority, "WorkMotelShift", true);
        Check(Yes(priority, "RequestOpen", 0) && !Yes(priority, "RequestOpen", 3), "younger urgent complaint takes priority over routine request");

        object portfolio = Activator.CreateInstance(Portfolio, BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { Activator.CreateInstance(Portfolio.GetNestedType("SaveData", BindingFlags.NonPublic), true) }, null);
        object motelId = EnumValue("PropertyId", "RoadsideMotel");
        Set(portfolio, "lastSavedMinute", 1440);
        Call(portfolio, "Advance", 1439, EnumValue("PropertyId", "None"), NewMotel());
        Check(!((IEnumerable)Property(portfolio, "Invoices")).Cast<object>().Any(), "no invoice before day boundary");
        Call(portfolio, "Advance", 1440, EnumValue("PropertyId", "None"), NewMotel());
        Check(((IEnumerable)Property(portfolio, "Invoices")).Cast<object>().Count() == 3, "three recurring utility invoices on schedule");
        Check(Yes(portfolio, "PayInvoices", motelId, false) && !Yes(portfolio, "PayInvoices", motelId, false),
            "recurring bills can be paid only once");
    }

    private static void BookedVehicleArrivals()
    {
        object service = NewMotel();
        Check(Book(service, 0, 770, 2100, "Telephone"), "phone reservation schedules a car arrival");
        Call(service, "AdvanceClock", 769);
        Check((int)Call(service, "NextArrival") == -1 &&
            !Yes(service, "CheckInWithStaff", 0, 769, 769), "early reservation cannot spawn or check in");
        Call(service, "AdvanceClock", 770);
        Check((int)Call(service, "NextArrival") == 0, "arrival selected at the scheduled minute");
        Call(service, "ModifyBooking", 0, 0);
        Check((int)Call(service, "NextArrival") == -1, "cancelled booking cannot produce a guest");

        object remote = NewMotel();
        Set(remote, "Cash", 500);
        Call(remote, "ManageStaff", 5, false);
        Call(remote, "WorkMotelShift", true);
        Check((int)Property(remote, "ActiveRoomCount") == 0, "remote receptionist cannot invent walk-ins");

        var carPose = Type("MotelWorld").GetMethod("GuestCarPose", BindingFlags.Static | BindingFlags.NonPublic);
        object driving = carPose.Invoke(null, new object[] { 0, 0f });
        object parked = carPose.Invoke(null, new object[] { 0, 1f });
        Vector3 start = (Vector3)Field(driving, "Position").GetValue(driving);
        Vector3 stall = (Vector3)Field(parked, "Position").GetValue(parked);
        Check(Vector3.Distance(start, stall) > 5 && Mathf.Abs(stall.z + 4.63f) < .01f,
            "arrival car moves from entrance to its parking stall");
    }

    private static void Routing()
    {
        Type view = Type("PropertyView");
        Type world = Type("MotelWorld");
        var routeMethod = view.GetMethod("QueueDestination", BindingFlags.Static | BindingFlags.NonPublic);
        var route = new System.Collections.Generic.Queue<Vector3>();
        Vector3 car = (Vector3)world.GetMethod("CarApproach").Invoke(null, new object[] { 0 });
        Vector3 reception = (Vector3)world.GetField("GuestSpawn").GetValue(null);
        Vector3 assigned = (Vector3)world.GetMethod("RoomApproach").Invoke(null, new object[] { 4 });
        routeMethod.Invoke(null, new object[] { route, car, reception });
        Check(route.Count > 2 && route.Last() == reception, "car route reaches reception through waypoints");
        routeMethod.Invoke(null, new object[] { route, reception, assigned });
        Check(route.Contains((Vector3)world.GetField("StairTop").GetValue(null)) && route.Last() == assigned,
            "reception route reaches assigned upper-floor room using stairs");
        Vector3 computer = (Vector3)world.GetField("ComputerApproach").GetValue(null);
        Vector3 front = (Vector3)world.GetField("DeskFrontCorner").GetValue(null);
        Vector3 back = (Vector3)world.GetField("DeskBackCorner").GetValue(null);
        routeMethod.Invoke(null, new object[] { route, reception, computer });
        Check(route.SequenceEqual(new[] { front, back, computer }), "computer route goes around the desk");
        routeMethod.Invoke(null, new object[] { route, computer, reception });
        Check(route.SequenceEqual(new[] { back, front, reception }), "desk exit follows the clear side aisle");
        var scene = new GameObject("Reception route test");
        object walker = null;
        try
        {
            Activator.CreateInstance(world, scene.transform, false);
            walker = Activator.CreateInstance(Type("MotelWalker"), scene.transform, "Caretaker", reception);
            Physics.SyncTransforms();
            routeMethod.Invoke(null, new object[] { route, reception, computer });
            var drive = view.GetMethod("Drive", BindingFlags.Static | BindingFlags.NonPublic);
            for (int step = 0; step < 480 && route.Count > 0; step++)
                drive.Invoke(null, new object[] { walker, route, Vector3.zero, false, 1f / 60f });
            Vector3 position = (Vector3)Property(walker, "Position");
            Check(Vector2.Distance(new Vector2(position.x, position.z), new Vector2(computer.x, computer.z)) < .25f,
                $"reception aisle has physical clearance for the player, reached {position}");
        }
        finally
        {
            if (walker != null)
                Call(walker, "Dispose");
            UnityEngine.Object.DestroyImmediate(scene);
        }
    }
}
