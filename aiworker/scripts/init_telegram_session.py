import asyncio
import os
from pathlib import Path

from dotenv import load_dotenv
from telethon import TelegramClient
from telethon.errors import PhoneNumberInvalidError
from telethon.sessions import StringSession


PROJECT_ROOT = Path(__file__).resolve().parents[1]
ENV_PATH = PROJECT_ROOT / ".env"
load_dotenv(dotenv_path=ENV_PATH)


def _normalize_phone(raw_value: str) -> str:
    value = raw_value.strip()
    value = value.replace(" ", "").replace("-", "").replace("(", "").replace(")", "")

    if value.startswith("00"):
        value = f"+{value[2:]}"
    elif value.isdigit():
        value = f"+{value}"

    return value


async def main() -> None:
    api_id_raw = os.getenv("TELEGRAM_API_ID", "0").strip()
    api_hash = os.getenv("TELEGRAM_API_HASH", "").strip()

    try:
        api_id = int(api_id_raw)
    except ValueError:
        api_id = 0

    if api_id <= 0 or not api_hash:
        raise RuntimeError("Set TELEGRAM_API_ID and TELEGRAM_API_HASH before running this script.")

    raw_phone = input("Please enter your phone in international format (example: +9986846074): ")
    phone = _normalize_phone(raw_phone)

    if not phone.startswith("+") or not phone[1:].isdigit() or len(phone) < 8:
        raise RuntimeError("Invalid phone format. Use international format like +998XXXXXXXXX.")

    session = StringSession()
    client = TelegramClient(session, api_id, api_hash)
    try:
        await client.start(phone=phone)
        session_string = client.session.save()
    except PhoneNumberInvalidError as exc:
        raise RuntimeError(
            "Telegram rejected this phone number. Use your account number in international format with country code, e.g. +998XXXXXXXXX."
        ) from exc
    finally:
        await client.disconnect()

    print("\nTELEGRAM_SESSION_STRING=")
    print(session_string)


if __name__ == "__main__":
    asyncio.run(main())
