namespace Linkora.Models
{

    public static class OmnivaEventCodes
    {

        public const string ShipmentRegistered = "TRT_SHIPMENT_REGISTERED";
        public const string ShipmentCancelled = "TRT_SHIPMENT_CANCELLED";
        public const string HandoverToParcelMachine = "TRT_SHIPMENT_HANDOVER_TO_PM";
        public const string PickedUpByCourier = "TRT_SHIPMENT_PICKED_UP_BY_COURIER";
        public const string HandoverToPostOffice = "TRT_SHIPMENT_HANDOVER_TO_PO";
        public const string DepartedFromParcelMachine = "TRT_SHIPMENT_DEPARTED_FROM_PM";
        public const string ArrivedToPostOffice = "TRT_SHIPMENT_ARRIVED_TO_PO";
        public const string CourierDeliveryStarted = "TRT_SHIPMENT_COURIER_DELIVERY_STARTED";
        public const string CourierCalledToReceiver = "TRT_COURIER_CALLED_TO_RECEIVER";
        public const string StoredInParcelMachine = "TRT_SHIPMENT_STORED_IN_PM";
        public const string ArrivedToFinalPostOffice = "TRT_SHIPMENT_ARRIVED_TO_FINAL_PO";
        public const string ArrivalPmNotification = "TRT_SHIPMENT_ARRIVAL_PM_NOTIFICATION";
        public const string ArrivalPoNotification = "TRT_SHIPMENT_ARRIVAL_PO_NOTIFICATION";
        public const string ReminderPoNotification = "TRT_SHIPMENT_REMINDER_PO_NOTIFICATION";
        public const string ReminderPmNotification = "TRT_SHIPMENT_REMINDER_PM_NOTIFICATION";
        public const string ReturnInitiated = "TRT_SHIPMENT_RETURN";
        public const string Redirected = "TRT_SHIPMENT_REDIRECTION";
        public const string FailedDeliveryAttempt = "TRT_SHIPMENT_FAILED_DELIVERY_ATTEMPT";
        public const string Delivered = "TRT_SHIPMENT_DELIVERED";
        public const string CourierDeliveryCancelled = "TRT_SHIPMENT_COURIER_DELIVERY_CANCELLED";
        public const string MovementStopped = "TRT_SHIPMENT_MOVEMENT_STOPPED";
        public const string MovementRestored = "TRT_SHIPMENT_MOVEMENT_RESTORED";
        public const string ReturnedByCustomer = "TRT_SHIPMENT_RETURNED_BY_CUSTOMER";
        public const string CourierPreDeliveryNotification = "TRT_COURIER_PRE_DELIVERY_RECIPIENT_NOTIFICATION";
        public const string MissingInParcelMachine = "TRT_SHIPMENT_MISSING_IN_PM";
        public const string RegisteredInOmnivaTerminal = "TRT_SHIPMENT_REGISTERED_IN_OMNIVA";
        public const string RecipientContactUpdated = "TRT_SHIPMENT_RECIPIENT_CONTACT_UPDATED";

        public const string IntlEda = "TRT_SHIPMENT_EDA";
        public const string IntlEdb = "TRT_SHIPMENT_EDB";
        public const string IntlEdc = "TRT_SHIPMENT_EDC";
        public const string IntlEdd = "TRT_SHIPMENT_EDD";
        public const string IntlEdf = "TRT_SHIPMENT_EDF";
        public const string IntlEdg = "TRT_SHIPMENT_EDG";
        public const string IntlEdh = "TRT_SHIPMENT_EDH";
        public const string IntlEdx = "TRT_SHIPMENT_EDX";
        public const string IntlEma = "TRT_SHIPMENT_EMA";
        public const string IntlEmb = "TRT_SHIPMENT_EMB";
        public const string IntlEmc = "TRT_SHIPMENT_EMC";
        public const string IntlEmd = "TRT_SHIPMENT_EMD";
        public const string IntlEmf = "TRT_SHIPMENT_EMF";
        public const string IntlEmg = "TRT_SHIPMENT_EMG";
        public const string IntlEmh = "TRT_SHIPMENT_EMH";
        public const string IntlEmi = "TRT_SHIPMENT_EMI";
        public const string IntlEmj = "TRT_SHIPMENT_EMJ";
        public const string IntlExx = "TRT_SHIPMENT_EXX";

        public const string LegacyArrivedAtSortingCentre = "TRT_ARRIVED_AT_SORTING_CENTRE";
        public const string LegacySentToDestination = "TRT_SENT_TO_DESTINATION";
        public const string LegacyArrivedAtParcelMachine = "TRT_ARRIVED_AT_PARCEL_MACHINE";
        public const string LegacyDelivered = "TRT_DELIVERED";
        public const string LegacyCustomsProcessing = "TRT_CUSTOMS_PROCESSING";

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
