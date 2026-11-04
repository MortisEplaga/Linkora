namespace Linkora.Models
{
    /// <summary>
    /// OMX tracking event codes, reconciled with "Possible events for event request" (Omniva, supplied as PDF).
    /// The catalogue is built into the code so tracking works even when Omniva:EventStatusMap is not configured;
    /// configuration entries with the same key override a default value.
    ///
    /// Codes not present in DefaultStatusMap are informational: they are still written to ShipmentEvents
    /// (LastEventCode / LastEventAt / RawJson) but deliberately do not change ShipmentStatus and do not send
    /// a notification. That covers arrival/reminder SMS, "movement stopped/restored", contact updates and
    /// customs milestones, which would otherwise regress Delivered or ArrivedAtLocker back to InTransit
    /// or spam the buyer.
    /// </summary>
    public static class OmnivaEventCodes
    {
        // --- Core lifecycle (PDF) ---
        public const string ShipmentRegistered = "TRT_SHIPMENT_REGISTERED";               // Shipment data registered
        public const string ShipmentCancelled = "TRT_SHIPMENT_CANCELLED";                // Shipment cancelled
        public const string HandoverToParcelMachine = "TRT_SHIPMENT_HANDOVER_TO_PM";     // Sender dropped off in parcel machine
        public const string PickedUpByCourier = "TRT_SHIPMENT_PICKED_UP_BY_COURIER";
        public const string HandoverToPostOffice = "TRT_SHIPMENT_HANDOVER_TO_PO";
        public const string DepartedFromParcelMachine = "TRT_SHIPMENT_DEPARTED_FROM_PM";
        public const string ArrivedToPostOffice = "TRT_SHIPMENT_ARRIVED_TO_PO";
        public const string CourierDeliveryStarted = "TRT_SHIPMENT_COURIER_DELIVERY_STARTED"; // Handed to courier for delivery
        public const string CourierCalledToReceiver = "TRT_COURIER_CALLED_TO_RECEIVER";
        public const string StoredInParcelMachine = "TRT_SHIPMENT_STORED_IN_PM";        // Arrived at PM for customer pickup
        public const string ArrivedToFinalPostOffice = "TRT_SHIPMENT_ARRIVED_TO_FINAL_PO"; // Arrived at PO for pickup
        public const string ArrivalPmNotification = "TRT_SHIPMENT_ARRIVAL_PM_NOTIFICATION";
        public const string ArrivalPoNotification = "TRT_SHIPMENT_ARRIVAL_PO_NOTIFICATION";
        public const string ReminderPoNotification = "TRT_SHIPMENT_REMINDER_PO_NOTIFICATION";
        public const string ReminderPmNotification = "TRT_SHIPMENT_REMINDER_PM_NOTIFICATION";
        public const string ReturnInitiated = "TRT_SHIPMENT_RETURN";                     // Return has been initiated
        public const string Redirected = "TRT_SHIPMENT_REDIRECTION";
        public const string FailedDeliveryAttempt = "TRT_SHIPMENT_FAILED_DELIVERY_ATTEMPT";
        public const string Delivered = "TRT_SHIPMENT_DELIVERED";                        // NOTE: NOT "TRT_DELIVERED"
        public const string CourierDeliveryCancelled = "TRT_SHIPMENT_COURIER_DELIVERY_CANCELLED";
        public const string MovementStopped = "TRT_SHIPMENT_MOVEMENT_STOPPED";
        public const string MovementRestored = "TRT_SHIPMENT_MOVEMENT_RESTORED";
        public const string ReturnedByCustomer = "TRT_SHIPMENT_RETURNED_BY_CUSTOMER";   // Returned by customer
        public const string CourierPreDeliveryNotification = "TRT_COURIER_PRE_DELIVERY_RECIPIENT_NOTIFICATION";
        public const string MissingInParcelMachine = "TRT_SHIPMENT_MISSING_IN_PM";
        public const string RegisteredInOmnivaTerminal = "TRT_SHIPMENT_REGISTERED_IN_OMNIVA"; // Arrived without preregistration
        public const string RecipientContactUpdated = "TRT_SHIPMENT_RECIPIENT_CONTACT_UPDATED";

        // --- Outbound international shipments (PDF, section "EVENTS ONLY FOR OUTBOUND INTERNATIONAL SHIPMENTS") ---
        public const string IntlEda = "TRT_SHIPMENT_EDA";      // Held at inward office of exchange
        public const string IntlEdb = "TRT_SHIPMENT_EDB";      // Presented to import border agency
        public const string IntlEdc = "TRT_SHIPMENT_EDC";      // Returned from border agency
        public const string IntlEdd = "TRT_SHIPMENT_EDD";      // Into sorting center
        public const string IntlEdf = "TRT_SHIPMENT_EDF";      // Held at delivery depot
        public const string IntlEdg = "TRT_SHIPMENT_EDG";      // Out for physical delivery
        public const string IntlEdh = "TRT_SHIPMENT_EDH";      // Arrival at collection point for pickup
        public const string IntlEdx = "TRT_SHIPMENT_EDX";      // Import termination
        public const string IntlEma = "TRT_SHIPMENT_EMA";      // Posting / collection
        public const string IntlEmb = "TRT_SHIPMENT_EMB";      // Arrival at outward office of exchange
        public const string IntlEmc = "TRT_SHIPMENT_EMC";      // Departure from outward office of exchange
        public const string IntlEmd = "TRT_SHIPMENT_EMD";      // Arrival to inward office of exchange
        public const string IntlEmf = "TRT_SHIPMENT_EMF";      // Departure from inward office of exchange
        public const string IntlEmg = "TRT_SHIPMENT_EMG";      // Arrival at delivery office
        public const string IntlEmh = "TRT_SHIPMENT_EMH";      // Unsuccessful physical delivery
        public const string IntlEmi = "TRT_SHIPMENT_EMI";      // Final delivery
        public const string IntlEmj = "TRT_SHIPMENT_EMJ";      // Arrival to transit country
        public const string IntlExx = "TRT_SHIPMENT_EXX";      // Export cancellation

        // --- Legacy aliases from the earlier short code list, kept so old queued events still map ---
        public const string LegacyArrivedAtSortingCentre = "TRT_ARRIVED_AT_SORTING_CENTRE";
        public const string LegacySentToDestination = "TRT_SENT_TO_DESTINATION";
        public const string LegacyArrivedAtParcelMachine = "TRT_ARRIVED_AT_PARCEL_MACHINE";
        public const string LegacyDelivered = "TRT_DELIVERED";
        public const string LegacyCustomsProcessing = "TRT_CUSTOMS_PROCESSING";

        /// <summary>
        /// Events proving that the parcel physically entered the carrier network. Used by the handover-expiry job:
        /// "shipment registered", "cancelled" and "contact updated" alone must not keep a parcel alive forever.
        /// </summary>
        public static readonly IReadOnlyList<string> HandoverCodes =
        [
            HandoverToParcelMachine, PickedUpByCourier, HandoverToPostOffice, DepartedFromParcelMachine,
            ArrivedToPostOffice, CourierDeliveryStarted, CourierCalledToReceiver, StoredInParcelMachine,
            ArrivedToFinalPostOffice, ArrivalPmNotification, ArrivalPoNotification,
            ReminderPmNotification, ReminderPoNotification, ReturnInitiated, Redirected,
            FailedDeliveryAttempt, Delivered, CourierDeliveryCancelled, MovementStopped,
            MovementRestored, ReturnedByCustomer, CourierPreDeliveryNotification,
            MissingInParcelMachine, RegisteredInOmnivaTerminal,
            IntlEda, IntlEdb, IntlEdc, IntlEdd, IntlEdf, IntlEdg, IntlEdh,
            IntlEma, IntlEmb, IntlEmc, IntlEmd, IntlEmf, IntlEmg, IntlEmh, IntlEmi, IntlEmj,
            LegacyArrivedAtSortingCentre, LegacySentToDestination, LegacyArrivedAtParcelMachine,
            LegacyDelivered, LegacyCustomsProcessing
        ];

        /// <summary>
        /// Default mapping to platform statuses. Failed is used for TRT_SHIPMENT_CANCELLED because
        /// ShipmentStatus has no Cancelled value. Returned is now reachable via TRT_SHIPMENT_RETURN and
        /// TRT_SHIPMENT_RETURNED_BY_CUSTOMER.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, ShipmentStatus> DefaultStatusMap =
            new Dictionary<string, ShipmentStatus>(StringComparer.OrdinalIgnoreCase)
            {
                [ShipmentRegistered] = ShipmentStatus.Registered,
                [ShipmentCancelled] = ShipmentStatus.Failed,

                [HandoverToParcelMachine] = ShipmentStatus.InTransit,
                [PickedUpByCourier] = ShipmentStatus.InTransit,
                [HandoverToPostOffice] = ShipmentStatus.InTransit,
                [DepartedFromParcelMachine] = ShipmentStatus.InTransit,
                [ArrivedToPostOffice] = ShipmentStatus.InTransit,
                [CourierDeliveryStarted] = ShipmentStatus.InTransit,
                [CourierCalledToReceiver] = ShipmentStatus.InTransit,
                [Redirected] = ShipmentStatus.InTransit,
                [FailedDeliveryAttempt] = ShipmentStatus.InTransit,
                [CourierDeliveryCancelled] = ShipmentStatus.InTransit,
                [CourierPreDeliveryNotification] = ShipmentStatus.InTransit,
                [RegisteredInOmnivaTerminal] = ShipmentStatus.InTransit,
                [IntlEda] = ShipmentStatus.InTransit,
                [IntlEdb] = ShipmentStatus.InTransit,
                [IntlEdc] = ShipmentStatus.InTransit,
                [IntlEdd] = ShipmentStatus.InTransit,
                [IntlEdf] = ShipmentStatus.InTransit,
                [IntlEdg] = ShipmentStatus.InTransit,
                [IntlEma] = ShipmentStatus.InTransit,
                [IntlEmb] = ShipmentStatus.InTransit,
                [IntlEmc] = ShipmentStatus.InTransit,
                [IntlEmd] = ShipmentStatus.InTransit,
                [IntlEmf] = ShipmentStatus.InTransit,
                [IntlEmg] = ShipmentStatus.InTransit,
                [IntlEmh] = ShipmentStatus.InTransit,
                [IntlEmj] = ShipmentStatus.InTransit,
                [LegacyArrivedAtSortingCentre] = ShipmentStatus.InTransit,
                [LegacySentToDestination] = ShipmentStatus.InTransit,
                [LegacyCustomsProcessing] = ShipmentStatus.InTransit,

                [StoredInParcelMachine] = ShipmentStatus.ArrivedAtLocker,
                [ArrivedToFinalPostOffice] = ShipmentStatus.ArrivedAtLocker,
                [IntlEdh] = ShipmentStatus.ArrivedAtLocker,
                [LegacyArrivedAtParcelMachine] = ShipmentStatus.ArrivedAtLocker,

                [Delivered] = ShipmentStatus.Delivered,
                [LegacyDelivered] = ShipmentStatus.Delivered,
                [IntlEmi] = ShipmentStatus.Delivered,

                [ReturnInitiated] = ShipmentStatus.Returned,
                [ReturnedByCustomer] = ShipmentStatus.Returned
            };
    }
}
