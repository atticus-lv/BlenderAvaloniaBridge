from . import operators, panel, preferences, properties


MODULES = (
    properties,
    preferences,
    operators,
    panel,
)


def register():
    for module in MODULES:
        module.register()


def unregister():
    for module in reversed(MODULES):
        module.unregister()
