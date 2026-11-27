using System;

public static partial class CoreGameplayChecks
{
    private static void Integration()
    {
        CheckoutToRoomReadiness();
        RequestsToSupplies();
        ReservationsToAvailability();
        ServiceToDemand();
        RenovationToPricing();
    }

    private static void CheckoutToRoomReadiness()
    {
        object service = NewMotel();
        Set(service, "Cash", 1000);
        Set(service, "CleaningKits", 1);
        Set(service, "TrashBags", 2);
        Set(service, "LinenSets", 4);
        Set(service, "Vacuums", 1);
        Check(CheckInReserved(service, 0, 720, 720), "integration: turnover check-in");
        Call(service, "CompleteCheckout", 0, true);
        Check(Yes(service, "TaskPending", 0, EnumValue("CleaningTask", "Trash")) &&
            !Yes(service, "RoomReadyForGuest", 0), "integration: checkout creates cleaning work and blocks assignment");
        Call(service, "WorkMotelShift", true);
        Check((int)Call(service, "Cleanliness", 0) == 0, "integration: work stays pending without housekeeper");
        int cash = Number(service, "Cash");
        Call(service, "ManageStaff", 2, false);
        Call(service, "WorkMotelShift", true);
        Check(Number(service, "Cash") < cash && Report(service, "Payroll") < 0 &&
            (int)Call(service, "Cleanliness", 0) > 0 && !Yes(service, "RoomReadyForGuest", 0),
            "integration: hiring adds payroll, completes work, and keeps partially cleaned room closed");
        Call(service, "WorkMotelShift", true);
        Call(service, "WorkMotelShift", true);
        Check(Yes(service, "RoomReadyForGuest", 0) && Number(service, "LinenSets") == 2,
            "integration: housekeeper completes turnover and releases room");
    }

    private static void RequestsToSupplies()
    {
        object service = NewMotel();
        Set(service, "Cash", 1000);
        Call(service, "AdvanceClock", 760);
        Check(CheckInReserved(service, 0, 760, 760), "integration: check in towel guest");
        Call(service, "ManageStaff", 1, false);
        Call(service, "ManageStaff", 3, false);
        Call(service, "AdvanceClock", 835);
        string notebook = (string)Call(service, "NotebookRequest", 0);
        Check(notebook.Contains("Deliver towels") && notebook.Contains("Waiting for supplies") &&
            Yes(service, "RequestDue", 0), "integration: towel request and shortage reach notebook and staff queue");
        Call(service, "WorkMotelShift", true);
        Check(Yes(service, "RequestDue", 0) && Number(service, "Towels") == 0,
            "integration: shortage leaves task pending and inventory unchanged");
        Check(Yes(service, "Order", 1), "integration: purchase towel delivery");
        Call(service, "AdvanceClock", 907);
        Call(service, "AdvanceDelivery");
        Call(service, "AdvanceClock", 942);
        Check(Number(service, "Towels") == 3 && (int)Property(service, "CompletedOrderCount") == 1 &&
            !Yes(service, "RequestDue", 0) && Call(service, "NotebookRequest", 0) == null &&
            ((string)Call(service, "GuestSatisfaction", 0)).Contains("Satisfied"),
            "integration: stocker delivers four towels; attendant uses one and clears the notebook task");
    }

    private static void ReservationsToAvailability()
    {
        object service = NewMotel();
        Check(Book(service, 0, 3000, 4000), "integration: calendar fixture booked");
        Check(!Yes(service, "RoomAvailable", 0, 2880, 4320, -1, false), "integration: calendar date blocked");
        Call(service, "ModifyBooking", 2, 0);
        Check(Yes(service, "RoomAvailable", 0, 2880, 4320, -1, false), "integration: cancellation reopens calendar date");
        Check(CheckInReserved(service, 0, 720, 720), "integration: extension fixture checked in");
        int departure = (int)Call(service, "DepartureMinute", 0);
        Check(Book(service, 0, departure + 30, departure + 600), "integration: future guest has confirmed dates");
        int income = Report(service, "RoomRevenue");
        Call(service, "ExtendStay", 0);
        Check((int)Call(service, "DepartureMinute", 0) == departure && Report(service, "RoomRevenue") == income,
            "integration: failed extension preserves departure and payment");
    }

    private static void ServiceToDemand()
    {
        object service = NewMotel();
        Set(service, "reputation", 55);
        int originalDemand = (int)Property(service, "Demand");
        Call(service, "AdvanceClock", 760);
        Check(CheckInReserved(service, 0, 650, 760), "integration: late-service guest checks in");
        var toilet = EnumValue("Fixture", "Toilet");
        Call(service, "Wear", 0, toilet, 100);
        Check(Call(service, "OutstandingIssue", 0).ToString() == "Equipment",
            "integration: broken equipment opens maintenance complaint");
        Call(service, "AdvanceClock", 890);
        Call(service, "AcknowledgeRequest", 0);
        Check(((string)Call(service, "GuestSatisfaction", 0)).Contains("Impatient"),
            "integration: poor request response shows impatience");
        Call(service, "CompleteCheckout", 0, true);
        Check(((string)Call(service, "GuestReview", 0)).Contains("unresolved") &&
            (float)Property(service, "CustomerStars") <= 2 && (int)Property(service, "Demand") < originalDemand,
            "integration: poor service lowers rating and demand");
        int before = (int)Property(service, "ReservationCount");
        Call(service, "AdvanceClock", 1440);
        int bookings = (int)Property(service, "ReservationCount") - before;

        object control = NewMotel();
        Set(control, "reputation", 55);
        int controlBefore = (int)Property(control, "ReservationCount");
        Call(control, "AdvanceClock", 1440);
        Check(bookings == 1 && (int)Property(control, "ReservationCount") - controlBefore == 2,
            "integration: poor reviews actually reduce generated bookings");
    }

    private static void RenovationToPricing()
    {
        object service = NewMotel();
        int quality = (int)Call(service, "RoomQuality", 0);
        int rate = (int)Call(service, "NightlyRate", 0);
        Check(Book(service, 0, 2400, 2800), "integration: booking retains original quote");
        Check(Yes(service, "ManageProperty", EnumValue("ManagementAction", "PlaceFurniture"), 0, 0),
            "integration: player purchases upgraded bedding");
        Check((int)Call(service, "RoomQuality", 0) > quality && (int)Call(service, "NightlyRate", 0) > rate &&
            (int)Call(service, "ReservationRate", 2) == rate && Report(service, "Maintenance") < 0,
            "integration: upgrade raises quality and future price while existing quote remains fixed");
    }
}
