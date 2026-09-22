namespace QuantumObliterator
{
    /// <summary>Outcome of a transfer attempt. Sent over the wire as an int; keep values stable.</summary>
    internal enum QOResult
    {
        Success = 0,
        NoPartner = 1,
        Ambiguous = 2,
        NoFuel = 3,
        DestinationFull = 4,
        Disabled = 5,
        Error = 6,
        Timeout = 7,
        NotServer = 8,
        Desync = 9,
        NothingToShip = 10,
    }

    /// <summary>What the server decided, and everything the client needs to render a message.</summary>
    internal struct TransferOutcome
    {
        internal QOResult Result;

        /// <summary>Context for the message: the tag, or a localisation token for the fuel item.</summary>
        internal string Detail;

        /// <summary>Fuel amount for NoFuel, or item count moved for Success.</summary>
        internal int Amount;

        internal ZDOID Destination;

        internal static TransferOutcome Fail(QOResult r, string detail = "", int amount = 0)
        {
            return new TransferOutcome { Result = r, Detail = detail ?? "", Amount = amount };
        }
    }

    internal static class QOTokens
    {
        internal const string Success = "$qo_msg_success";
        internal const string NoPartner = "$qo_msg_nopartner";
        internal const string Ambiguous = "$qo_msg_ambiguous";
        internal const string NoFuel = "$qo_msg_nofuel";
        internal const string Full = "$qo_msg_full";
        internal const string Error = "$qo_msg_error";
        internal const string Timeout = "$qo_msg_timeout";
        internal const string Disabled = "$qo_msg_disabled";
        internal const string Desync = "$qo_msg_desync";
        internal const string NothingToShip = "$qo_msg_nothingtoship";

        /// <summary>Lever hover text prefix; carries a {tag} placeholder.</summary>
        internal const string HoverShipTo = "$qo_hover_shipto";

        internal static string For(QOResult r)
        {
            switch (r)
            {
                case QOResult.Success: return Success;
                case QOResult.NoPartner: return NoPartner;
                case QOResult.Ambiguous: return Ambiguous;
                case QOResult.NoFuel: return NoFuel;
                case QOResult.DestinationFull: return Full;
                case QOResult.Disabled: return Disabled;
                case QOResult.Timeout: return Timeout;
                case QOResult.Desync: return Desync;
                case QOResult.NothingToShip: return NothingToShip;
                default: return Error;
            }
        }
    }
}
