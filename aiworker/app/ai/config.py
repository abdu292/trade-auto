import os
from typing import Dict
from app.ai.providers.base_provider import AIProviderConfig

# Load API keys from environment variables
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
GROK_API_KEY = os.getenv("GROK_API_KEY")  # xAI API key
PERPLEXITY_API_KEY = os.getenv("PERPLEXITY_API_KEY")

# Configure enabled providers
AI_PROVIDERS: Dict[str, AIProviderConfig] = {}

if OPENAI_API_KEY:
    AI_PROVIDERS["openai"] = AIProviderConfig(
        name="openai",
        api_key=OPENAI_API_KEY,
        model="gpt-4-turbo",  # or "gpt-4", "gpt-3.5-turbo"
        temperature=0.6,
        max_tokens=500,
        timeout=10
    )

if GROK_API_KEY:
    AI_PROVIDERS["grok"] = AIProviderConfig(
        name="grok",
        api_key=GROK_API_KEY,
        model="grok-beta",
        temperature=0.6,
        max_tokens=500,
        timeout=10
    )

if PERPLEXITY_API_KEY:
    AI_PROVIDERS["perplexity"] = AIProviderConfig(
        name="perplexity",
        api_key=PERPLEXITY_API_KEY,
        model="pplx-70b-online",  # or "pplx-70b"
        temperature=0.6,
        max_tokens=500,
        timeout=10
    )

# Strategy: fallback|consensus
# - fallback: try primary, then secondary, etc. (faster, less cost)
# - consensus: get multiple opinions (slower, higher confidence)
AI_STRATEGY = os.getenv("AI_STRATEGY", "fallback")

# For consensus mode, minimum providers that must agree
CONSENSUS_MIN_AGREEMENT = int(os.getenv("CONSENSUS_MIN_AGREEMENT", "2"))
