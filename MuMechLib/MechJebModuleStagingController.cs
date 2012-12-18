﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MuMech
{
    public class MechJebModuleStagingController : ComputerModule
    {
        public MechJebModuleStagingController(MechJebCore core)
            : base(core)
        {
            priority = 1000;
        }

        private bool _AutoStage = false;
        public bool AutoStage
        {
            get { return _AutoStage; }
            set
            {
                _AutoStage = value;
                //add settings saving
            }
        }

        private double _AutoStageDelay = 1.0;
        public double AutoStageDelay
        {
            get { return _AutoStageDelay; }
            set
            {
                _AutoStageDelay = value;
                //add settings saving
            }
        }

        private int _AutoStageLimit = 0;
        public int AutoStageLimit
        {
            get { return _AutoStageLimit; }
            set
            {
                _AutoStageLimit = value;
                //add settings saving
            }
        }

        double lastStageTime = 0;

        public override void OnFixedUpdate()
        {
            if (!part.vessel.isActiveVessel) return;

            //if autostage enabled, and if we are not waiting on the pad, and if there are stages left,
            //and if we are allowed to continue staging, and if we didn't just fire the previous stage
            if (AutoStage && part.vessel.LiftedOff() && Staging.CurrentStage > 0 && Staging.CurrentStage > AutoStageLimit
                && vesselState.time - lastStageTime > AutoStageDelay)
            {
                //don't decouple active or idle engines or tanks
                if (!inverseStageDecouplesActiveOrIdleEngineOrTank(Staging.CurrentStage - 1, part.vessel))
                {
                    //only fire decouplers to drop deactivated engines or tanks
                    bool firesDecoupler = inverseStageFiresDecoupler(Staging.CurrentStage - 1, part.vessel);
                    if (!firesDecoupler
                        || inverseStageDecouplesDeactivatedEngineOrTank(Staging.CurrentStage - 1, part.vessel))
                    {
                        if (firesDecoupler)
                        {
                            //if we decouple things, delay the next stage a bit to avoid exploding the debris
                            lastStageTime = vesselState.time;
                        }

                        Staging.ActivateNextStage();
                    }
                }
            }
        }

        //determine whether it's safe to activate inverseStage
        public static bool inverseStageDecouplesActiveOrIdleEngineOrTank(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage && p.IsDecoupler() && hasActiveOrIdleEngineOrTankDescendant(p))
                {
                    return true;
                }
            }
            return false;
        }

        //detect if a part is above an active or idle engine in the part tree
        public static bool hasActiveOrIdleEngineOrTankDescendant(Part p)
        {
            if ((p.State == PartStates.ACTIVE || p.State == PartStates.IDLE)
                && p.IsEngine() && !p.IsSepratron() && p.EngineHasFuel())
            {
                return true; // TODO: properly check if ModuleEngines is active
            }
            if ((p is FuelTank) && (((FuelTank)p).fuel > 0)) return true;
            if (!p.IsSepratron())
            {
                foreach (PartResource r in p.Resources)
                {
                    if (r.amount > 0 && r.info.name != "ElectricCharge")
                    {
                        return true;
                    }
                }
            }
            foreach (Part child in p.children)
            {
                if (hasActiveOrIdleEngineOrTankDescendant(child)) return true;
            }
            return false;
        }

        //determine whether activating inverseStage will fire any sort of decoupler. This
        //is used to tell whether we should delay activating the next stage after activating inverseStage
        public static bool inverseStageFiresDecoupler(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage && p.IsDecoupler()) return true;
            }
            return false;
        }

        //determine whether inverseStage sheds a dead engine
        public static bool inverseStageDecouplesDeactivatedEngineOrTank(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage && p.IsDecoupler() && hasDeactivatedEngineOrTankDescendant(p)) return true;
            }
            return false;
        }

        //detect if a part is above a deactivated engine or fuel tank
        public static bool hasDeactivatedEngineOrTankDescendant(Part p)
        {
            if ((p.State == PartStates.DEACTIVATED) && (p is FuelTank || p.IsEngine()) && !p.IsSepratron())
            {
                return true; // TODO: yet more ModuleEngine lazy checks
            }

            //check if this is a new-style fuel tank that's run out of resources:
            bool hadResources = false;
            bool hasResources = false;
            foreach (PartResource r in p.Resources)
            {
                if (r.name == "ElectricCharge") continue;
                if (r.maxAmount > 0) hadResources = true;
                if (r.amount > 0) hasResources = true;
            }
            if (hadResources && !hasResources) return true;

            if (p.IsEngine() && !p.EngineHasFuel()) return true;

            foreach (Part child in p.children)
            {
                if (hasDeactivatedEngineOrTankDescendant(child)) return true;
            }
            return false;
        }
    }
}
