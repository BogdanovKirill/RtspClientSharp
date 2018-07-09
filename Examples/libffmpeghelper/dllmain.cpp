// dllmain.cpp : Defines the entry point for the DLL application.

#include "stdafx.h"

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
					 )
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		avcodec_register_all();
#if defined(_DEBUG)
		av_log_set_level(AV_LOG_VERBOSE);
#else
		av_log_set_level(AV_LOG_PANIC);
#endif
		break;
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}