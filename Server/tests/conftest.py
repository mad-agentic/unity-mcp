import pytest


@pytest.fixture(autouse=True)
def reset_singletons():
    """Reset global singletons after each test."""
    yield
    from core.config import reset_config
    from transport.plugin_hub import reset_plugin_hub
    from services.registry.registry import reset_groups, _registered_tools, _registered_resources

    reset_config()
    reset_plugin_hub()
    reset_groups()
    _registered_tools.clear()
    _registered_resources.clear()
