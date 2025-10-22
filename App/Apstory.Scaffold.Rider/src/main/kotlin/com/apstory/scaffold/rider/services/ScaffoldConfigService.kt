package com.apstory.scaffold.rider.services

import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage
import com.intellij.openapi.components.service
import com.intellij.util.xmlb.XmlSerializerUtil

@State(
    name = "ApstoryScaffoldConfig",
    storages = [Storage("ApstoryScaffoldConfig.xml")]
)
class ScaffoldConfigService : PersistentStateComponent<ScaffoldConfigService.State> {

    private var myState = State()

    data class State(
        var sqlDestination: String = "",
        var powershellScript: String = "gen-typescript.ps1"
    )

    override fun getState(): State = myState

    override fun loadState(state: State) {
        XmlSerializerUtil.copyBean(state, myState)
    }

    companion object {
        fun getInstance(): ScaffoldConfigService = service()
    }
}
