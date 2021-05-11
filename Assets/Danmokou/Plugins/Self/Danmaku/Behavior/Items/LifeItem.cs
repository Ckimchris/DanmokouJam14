﻿using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using UnityEngine;

namespace DMK.Behavior.Items {
public class LifeItem : Item {
    protected override ItemType Type => ItemType.LIFE;
    protected override short RenderOffsetIndex => 5;
    protected override float RotationTurns => 2;

    protected override void CollectMe() {
        GameManagement.Instance.AddLifeItems(1);
        base.CollectMe();
    }
}
}